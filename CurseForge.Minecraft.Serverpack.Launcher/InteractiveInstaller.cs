using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Spectre.Console;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static async Task<int> InteractiveInstallation(bool? automaticInstaller, uint? projectId, string fileId)
		{
			if (!CheckRequiredDependencies())
			{
				return -1;
			}

			if (automaticInstaller.HasValue && automaticInstaller.Value)
			{
				Console.WriteLine("Automatic modpack server installer activated");
				Console.WriteLine("ProjectId: {0}, FileId: {1}", projectId, fileId);
			}

			Console.WriteLine("Activating interactive mode. Please follow the instructions.");
			Console.WriteLine("If you want to know other ways to use this, please use the argument --help");
			Console.WriteLine();

			AnsiConsole.Write(new FigletText("CurseForge Server Installer").LeftAligned().Color(Color.Orange1));

			AnsiConsole.Write(new Rule("Interactive server installer"));

			if (!AnsiConsole.Confirm("Do you want to continue with the installation?"))
			{
				Console.WriteLine("Exiting application!");
				return 1;
			}
			Console.WriteLine();

			AnsiConsole.Write(new Rule("Selecting server path"));

			var defaultPath = (OperatingSystem.IsWindows() ? @"C:\mc-server" : "/mc-server");

			var serverPath = AnsiConsole.Prompt(
				new TextPrompt<string>(
					"Enter the [orange1 bold]path[/]"
				)
				.DefaultValue(defaultPath)
				.ValidationErrorMessage("[bold red]The entered path is not a valid directory path[/]")
				.Validate(path => {
					return TryDirectoryPath(path) ? ValidationResult.Success() : ValidationResult.Error();
				})
			);

			AnsiConsole.WriteLine("Server will be installed in {0}", serverPath);

			Console.WriteLine();

			GetCfApiInformation(out var cfApiKey, out var cfPartnerId, out var cfContactEmail, out var errors);

			if (errors.Count > 0)
			{
				AnsiConsole.MarkupLine("[bold red]Please resolve the errors before continuing.[/]");
				return -1;
			}

			using ApiClient cfApiClient = new(cfApiKey, cfPartnerId, cfContactEmail);

			if (!projectId.HasValue)
			{
				AnsiConsole.Write(new Rule("Search modpack to install"));

				var searchType = AnsiConsole.Prompt(new SelectionPrompt<string>()
					.Title("Do you want to search by [orange1 bold]project id[/] or [orange1 bold]project name[/]?")
					.AddChoices(new[]
					{
					"Project Id",
					"Project Name"
					})
					.HighlightStyle(new Style(Color.Orange1)));

				Console.WriteLine($"Searching with {searchType}");

				try
				{
					await cfApiClient.GetGamesAsync();
				}
				catch
				{
					Console.WriteLine("Error: Could not contact the CurseForge API, please check your API key");
					return -1;
				}

				if (searchType == "Project Id")
				{
					while (!await HandleProjectIdSearch(cfApiClient))
					{ }
				}
				else
				{
					while (!await HandleProjectSearch(cfApiClient))
					{ }
				}
			}
			else
			{
				var _selectedMod = await cfApiClient.GetModAsync(projectId.Value);
				if (_selectedMod?.Data == null)
				{
					Console.Write($"Error: Project {projectId} does not exist");
					return -1;
				}

				selectedMod = _selectedMod.Data;

				if (fileId == "latest")
				{
					var versions = await cfApiClient.GetModFilesAsync(selectedMod.Id);
					var validVersions = versions.Data.Where(v => v.FileStatus == APIClient.Models.Files.FileStatus.Approved);

					var latestVersion = validVersions.OrderByDescending(c => c.FileDate).First();

					selectedVersion = latestVersion;
				}
				else
				{
					if (!uint.TryParse(fileId, out var _fileId))
					{
						Console.WriteLine("Error: Use either \"latest\" or a file id for the version");
						return -1;
					}
					var _selectedFile = await cfApiClient.GetModFileAsync(projectId.Value, _fileId);
					if (_selectedFile?.Data == null)
					{
						Console.Write($"Error: File {fileId} does not exist");
						return -1;
					}

					selectedVersion = _selectedFile.Data;
				}
			}

			if (selectedMod == null)
			{
				Console.WriteLine("No modpack selected, aborting installation");
				return -1;
			}

			if (selectedVersion == null)
			{
				while (!await HandleProjectVersionSearch(cfApiClient, selectedMod))
				{ }
			}

			if (selectedVersion == null)
			{
				Console.WriteLine("No modpack version selected, aborting installation");
				return -1;
			}

			if (!AnsiConsole.Confirm($"Do you want to install {selectedMod.Name}, with version {selectedVersion.DisplayName}, into {serverPath}?"))
			{
				Console.WriteLine("Well, ok then. Bye!");
				return 1;
			}

			var startServer = AnsiConsole.Confirm("Do you want to start the server directly?");

			var javaArgs = AnsiConsole.Ask<string>("Do you want any [orange1 bold]java arguments[/] for the server?", "-Xms4G -Xmx4G");

			await InstallServer(selectedMod.Id, selectedVersion.Id, serverPath, javaArgs, startServer);

			await Task.Delay(500);

			return 1;
		}

		private static async Task<bool> HandleProjectVersionSearch(ApiClient cfApiClient, Mod selectedMod)
		{
			AnsiConsole.Write(new Rule($"Version selection of {selectedMod.Name} to install"));

			var versions = await AnsiConsole.Status()
				.StartAsync("Fetching versions", async ctx => {
					return await cfApiClient.GetModFilesAsync(selectedMod.Id);
				});

			// Only show approved files for download
			var validVersions = versions.Data.Where(v => v.FileStatus == APIClient.Models.Files.FileStatus.Approved);

			var versionSelection = AnsiConsole.Prompt(
				new SelectionPrompt<APIClient.Models.Files.File>()
				.Title("Select [orange1 bold]modpack version[/]")
				.MoreChoicesText("[grey](Move up and down to reveal more versions)[/]")
				.AddChoices(validVersions)
				.UseConverter(v => $"{v.DisplayName} ({v.ReleaseType}, {string.Join(", ", v.SortableGameVersions.Select(gv => gv.GameVersionName))}) Released {v.FileDate.Date.ToShortDateString()}")
				.HighlightStyle(new Style(Color.Orange1))
			);

			if (AnsiConsole.Confirm($"{versionSelection.DisplayName} selected, is this the correct version?"))
			{
				selectedVersion = versionSelection;
				return true;
			}

			return false;
		}

		private static async Task<bool> HandleProjectSearch(ApiClient cfApiClient)
		{
			var searchFilter = AnsiConsole.Prompt(
				new TextPrompt<string>(
					"[orange1 bold]Enter search terms[/]"
				)
			);

			var searchResults = await AnsiConsole.Status()
				.StartAsync("Searching for modpacks", async ctx => {
					List<Mod> modsFound = new List<Mod>();
					var modResults = await cfApiClient.SearchModsAsync(432, 4471, searchFilter: searchFilter, sortField: ModsSearchSortField.Popularity, sortOrder: ModsSearchSortOrder.Descending);
					await Task.Delay(250);
					modsFound.AddRange(modResults.Data);

					if (modResults.Pagination.TotalCount > modResults.Pagination.ResultCount)
					{
						uint index = modResults.Pagination.Index;
						while (modsFound.Count < modResults.Pagination.TotalCount)
						{
							ctx.Status($"Fetching more results ({modResults.Pagination.PageSize * (index + 1)} / {modResults.Pagination.TotalCount})");
							var moreResults = await cfApiClient.SearchModsAsync(432, 4471, searchFilter: searchFilter, sortField: ModsSearchSortField.Popularity, sortOrder: ModsSearchSortOrder.Descending, index: index++);
							modsFound.AddRange(moreResults.Data);
							await Task.Delay(250);
						}
					}

					ctx.Status("All data fetched");

					await Task.Delay(1000);

					return modsFound;
				});

			var modSelection = AnsiConsole.Prompt(
						new SelectionPrompt<Mod>()
						.Title("Select [orange1 bold]modpack[/]")
						.MoreChoicesText("[grey](Move up and down to reveal more modpacks)[/]")
						.AddChoices(searchResults)
						.UseConverter(m => m.Name.Replace("[", "[[").Replace("]", "]]"))
						.HighlightStyle(new Style(Color.Orange1))
					);

			if (AnsiConsole.Confirm($"{modSelection.Name} selected, is this the correct modpack?"))
			{
				selectedMod = modSelection;
				return true;
			}

			return false;
		}

		private static async Task<bool> HandleProjectIdSearch(ApiClient cfApiClient)
		{
			var projectId = AnsiConsole.Prompt(
					new TextPrompt<uint>(
						"Enter [orange1 bold]Project Id[/] of the modpack"
					).ValidationErrorMessage("Please enter a valid [orange1 bold]Project Id[/] for a modpack from CurseForge")
					.Validate(l => l > 0 ? ValidationResult.Success() : ValidationResult.Error("[orange1 bold]Project Ids[/] cannot be negative"))
				);

			var modInfo = await AnsiConsole.Status()
				.StartAsync("Fetching modpack information", async ctx => {
					return await cfApiClient.GetModAsync(projectId);
				});

			if (modInfo != null && modInfo.Data.Categories.Any(c => c.ClassId == 4471))
			{
				if (AnsiConsole.Confirm($"{modInfo.Data.Name} by {modInfo.Data.Authors?.FirstOrDefault()?.Name} selected, is this the correct modpack?"))
				{
					selectedMod = modInfo.Data;
					return true;
				}

				return false;
			}
			else
			{
				Console.WriteLine("No modpack found with that Id");
				selectedMod = null;

				return false;
			}
		}
	}
}
