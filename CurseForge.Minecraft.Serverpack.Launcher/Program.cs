using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Spectre.Console;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private const string MinecraftVersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
		private const string MinecraftJavaManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

		private const string FabricInstallerUrl = "https://meta.fabricmc.net/v2/versions/installer";

		private static async Task<int> Main(params string[] args)
		{
			var command = SetupCommand();
			if (args.Length == 0)
			{
				return await command.InvokeAsync("--help");
			}

			return await command.InvokeAsync(args);
		}

		private static RootCommand SetupCommand()
		{
			RootCommand command = new(
				description: @"Installs a CurseForge Modpack as a server as far as it can.

Example:
  cf-mc-server 477455 3295539 ""c:\mc-server""
  -- Installs the modpack ""Too Many Projects"", into the ""mc-server"" folder in the C-drive"
			);

			SetupSubcommand(command); ;
			SetupArguments(command);
			SetupOptions(command);

			command.Handler = CommandHandler.Create<int, int, string, string, bool>(async (projectid, fileid, serverPath, javaArgs, startServer) => {
				return await InstallServer(projectid, fileid, serverPath, javaArgs, startServer);
			});

			return command;
		}

		private static void SetupSubcommand(RootCommand command)
		{
			var interactive = new Command("interactive",
				description: @"The interactive mode lets you search and select what modpack you want to use.
This will search for modpacks from CurseForge.");

			interactive.Handler = CommandHandler.Create(async () => {
				return await InteractiveInstallation();
			});

			command.Add(interactive);
		}

		private async static Task<int> InteractiveInstallation()
		{
			if (!CheckRequiredDependencies())
			{
				return -1;
			}

			Console.WriteLine("Activating interactive mode. Please follow the instructions.");
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

			AnsiConsole.Write("Server will be installed in {0}", serverPath);

			Console.WriteLine();

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

			GetCfApiInformation(out var cfApiKey, out var cfPartnerId, out var cfContactEmail);
			using ApiClient cfApiClient = new(cfApiKey, cfPartnerId, cfContactEmail);

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

			if (selectedMod == null)
			{
				Console.WriteLine("No modpack selected, aborting installation");
				return -1;
			}

			while (!await HandleProjectVersionSearch(cfApiClient, selectedMod))
			{ }

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

			var javaArgs = string.Empty;
			var startServer = AnsiConsole.Confirm("Do you want to start the server directly?");

			if (startServer)
			{
				javaArgs = AnsiConsole.Ask<string>("Do you want any [orange1 bold]java arguments[/] for the server?");
			}

			await InstallServer(selectedMod.Id, selectedVersion.Id, serverPath, javaArgs, startServer);

			await Task.Delay(500);

			return 1;
		}

		private static Mod selectedMod = null;
		private static APIClient.Models.Files.File selectedVersion = null;

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
					var modResults = await cfApiClient.SearchModsAsync(432, 4471, searchFilter: searchFilter, sortField: ModsSearchSortField.Popularity, sortOrder: true);
					await Task.Delay(250);
					modsFound.AddRange(modResults.Data);

					if (modResults.Pagination.TotalCount > modResults.Pagination.ResultCount)
					{
						int index = modResults.Pagination.Index;
						while (modsFound.Count < modResults.Pagination.TotalCount)
						{
							ctx.Status($"Fetching more results ({modResults.Pagination.PageSize * (index + 1)} / {modResults.Pagination.TotalCount})");
							var moreResults = await cfApiClient.SearchModsAsync(432, 4471, searchFilter: searchFilter, sortField: ModsSearchSortField.Popularity, sortOrder: true, index: index++);
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
					new TextPrompt<int>(
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
				if (AnsiConsole.Confirm($"{modInfo.Data.Name} selected, is this the correct modpack?"))
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

		private static bool TryDirectoryPath(string path)
		{
			try
			{
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path).Delete();
				}

				return Path.IsPathFullyQualified(path);
			}
			catch
			{
				return false;
			}
		}

		private static void SetupOptions(RootCommand command)
		{
			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--projectid",
						"-pid"
					},
					description: "Sets the project id / modpack id to use",
					argumentType: typeof(long)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--fileid",
						"-fid"
					},
					description: "Sets the file id to use",
					argumentType: typeof(long)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--server-path",
						"-sp"
					},
					description: "Sets the server path, where to install the modpack server",
					argumentType: typeof(string)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[] {
						"--java-args",
						"-ja"
					},
					description: "Sets the java arguments to be used when launching the Minecraft server",
					argumentType: typeof(string)
				)
			);

			command.AddGlobalOption(
				new(
					aliases: new[]
					{
						"--start-server",
						"-start"
					},
					description: "Makes the server start when it's done installing the server and modpack",
					argumentType: typeof(bool)
				)
			);
		}

		private static void SetupArguments(RootCommand command)
		{
			command.AddArgument(new("projectid")
			{
				ArgumentType = typeof(int),
				Arity = ArgumentArity.ZeroOrOne,
				Description = "Sets the project id / modpack id to use",
			});

			command.AddArgument(new("fileid")
			{
				ArgumentType = typeof(int),
				Description = "Sets the file id to use"
			});

			command.AddArgument(new("server-path")
			{
				ArgumentType = typeof(string),
				Description = "Sets the server path, where to install the modpack server"
			});

			command.AddArgument(new("java-arguments")
			{
				ArgumentType = typeof(string),
				Arity = ArgumentArity.ZeroOrOne,
				Description = "Sets the java arguments to be used when launching the Minecraft server"
			});
		}

		private static async Task<int> InstallServer(int modId, int fileId, string path, string javaArgs, bool startServer)
		{
			GetCfApiInformation(out var cfApiKey, out var cfPartnerId, out var cfContactEmail);

			if (!CheckRequiredDependencies())
			{
				return -1;
			}

			Console.WriteLine($"Using project: {modId}");
			Console.WriteLine($"Using file: {fileId}");

			if (string.IsNullOrWhiteSpace(path))
			{
				Console.WriteLine("Missing installation path, please provide a path, like this \"C:\\minecraft-server\"");
				return -1;
			}

			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
				if (!Directory.Exists(path))
				{
					Console.WriteLine("Could not create installation directory, please provide a path you have permissions to use/access");
					return -1;
				}

				Console.WriteLine($"Created installation directory: \"{path}\"");
			}

			Console.WriteLine($"Using path: {path}");

			using (ApiClient cfApiClient = new(cfApiKey, cfPartnerId, cfContactEmail))
			{
				try
				{
					await cfApiClient.GetGamesAsync();
				}
				catch
				{
					Console.WriteLine("Error: Could not contact the CurseForge API, please check your API key");
					return -1;
				}

				var modLoader = MinecraftModloader.Unknown;
				var modInfo = await cfApiClient.GetModAsync(modId);

				Console.WriteLine($"Getting information about {modInfo.Data.Name}");

				if (!modInfo.Data.Categories.Any(c => c.ClassId == 4471))
				{
					// Not a modpack
					Console.WriteLine($"Error: Project is not a modpack, not allowed in current version of server launcher");
					return -1;
				}

				var modFile = await cfApiClient.GetModFileAsync(modId, fileId);

				var dlPath = Path.Combine(path, modFile.Data.FileName);
				var installPath = Path.Combine(path, "installed", modInfo.Data.Slug);
				var manifestPath = Path.Combine(installPath, "manifest.json");

				if (!File.Exists(dlPath))
				{
					using WebClient wc = new();
					await wc.DownloadFileTaskAsync(modFile.Data.DownloadUrl, dlPath);
				}

				if (!Directory.Exists(installPath))
				{
					ZipFile.ExtractToDirectory(dlPath, installPath, true);

					var overrides = Path.Combine(installPath, "overrides");

					if (Directory.Exists(overrides))
					{
						var allFiles = Directory.GetFiles(overrides, "*.*", SearchOption.AllDirectories);
						foreach (var file in allFiles)
						{
							var overrideFile = new FileInfo(file);
							var newFilePath = new FileInfo(overrideFile.FullName.Replace($"{Path.DirectorySeparatorChar}overrides{Path.DirectorySeparatorChar}", Path.DirectorySeparatorChar.ToString()));
							Directory.CreateDirectory(newFilePath.Directory.FullName);
							File.Copy(overrideFile.FullName, newFilePath.FullName, true);
						}
					}
				}

				var manifest = JsonSerializer.Deserialize<MinecraftManifest>(File.ReadAllText(manifestPath));

				using HttpClient hc = new();
				hc.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", cfApiKey);
				var gameVersionManifest = await hc.GetFromJsonAsync<MinecraftVersionManifest>(MinecraftVersionManifestUrl);
				var mcVersion = gameVersionManifest.Versions.FirstOrDefault(version => version.Id == manifest.Minecraft.Version);

				if (mcVersion == null)
				{
					Console.WriteLine($"Error: Could not find version {manifest.Minecraft.Version}, exiting");
					return -1;
				}

				var mcVersionInfo = await hc.GetFromJsonAsync<MinecraftVersionInfo>(mcVersion.Url);
				await DownloadJREAsync(hc, installPath, mcVersionInfo.JavaVersion);

				var modloaderVersion = string.Empty;
				var minecraftVersion = string.Empty;

				if (manifest.Minecraft.ModLoaders.Any(ml => ml.Id.StartsWith("fabric-") && ml.Primary))
				{
					foreach (var modloader in manifest.Minecraft.ModLoaders.Where(ml => ml.Id.StartsWith("fabric-") && ml.Primary))
					{
						var modloaderInfo = await GetLoaderDependencies<FabricModLoaderInfo>(cfApiClient, manifest.Minecraft.Version, modloader.Id);
						await DownloadLoaderDependencies(hc, installPath, modloaderInfo);
						modloaderVersion = modloaderInfo.NonMapped["forgeVersion"].ToString();
						minecraftVersion = modloaderInfo.NonMapped["minecraftVersion"].ToString();
					}
					modLoader = MinecraftModloader.Fabric;
				}

				if (manifest.Minecraft.ModLoaders.Any(ml => ml.Id.StartsWith("forge-") && ml.Primary))
				{
					foreach (var modloader in manifest.Minecraft.ModLoaders.Where(ml => ml.Id.StartsWith("forge-") && ml.Primary))
					{
						var modloaderInfo = await GetLoaderDependencies<ForgeModLoaderInfo>(cfApiClient, manifest.Minecraft.Version, modloader.Id);
						await DownloadLoaderDependencies(hc, installPath, modloaderInfo);
						modloaderVersion = modloaderInfo.NonMapped["forgeVersion"].ToString();
						minecraftVersion = modloaderInfo.NonMapped["minecraftVersion"].ToString();
					}

					modLoader = MinecraftModloader.Forge;
				}

				if (modLoader == MinecraftModloader.Unknown)
				{
					Console.WriteLine("Error: Could not determine modloader, bailing out");
					return -1;
				}

				var downloadItems = new List<LibraryDownloadItem>();

				foreach (var lib in mcVersionInfo.Libraries)
				{
					downloadItems.AddRange(lib.GetDownloadItems() ?? new());
				}

				using (WebClient wc = new())
				{
					foreach (var asset in downloadItems)
					{
						var installDir = Path.Combine(installPath, "libraries", asset.FilePath);
						Directory.CreateDirectory(installDir);

						var fileDownloadPath = Path.Combine(installDir, asset.Url.Segments.Last());
						if (!File.Exists(fileDownloadPath))
						{
							Console.WriteLine($"Downloading: {fileDownloadPath}");
							await wc.DownloadFileTaskAsync(asset.Url, fileDownloadPath);
						}
					}

					Console.WriteLine("Downloaded all required assets for Minecraft");

					var serverJar = Path.Combine(installPath, "server.jar");
					if (!File.Exists(serverJar))
					{
						Console.WriteLine("Downloading the server file");
						await wc.DownloadFileTaskAsync(mcVersionInfo.Downloads.Server.Url, serverJar);
						Console.WriteLine("Server.jar downloaded");
					}

					Console.WriteLine("Fixing EULA for you");
					await File.WriteAllTextAsync(Path.Combine(installPath, "eula.txt"), "eula=true");

					Console.WriteLine("Downloading mods for modpack");
					foreach (var file in manifest.Files)
					{
						var mod = await cfApiClient.GetModFileAsync((int)file.ProjectId, (int)file.FileId);
						var modPath = Path.Combine(installPath, "mods", mod.Data.FileName);
						Directory.CreateDirectory(Path.GetDirectoryName(modPath));
						if (!File.Exists(modPath))
						{
							Console.WriteLine($"Downloading (mods): {mod.Data.DisplayName} ({mod.Data.FileName})");
							await wc.DownloadFileTaskAsync(mod.Data.DownloadUrl, modPath);
						}
					}
				}

				switch (modLoader)
				{
					case MinecraftModloader.Fabric:
						await InstallFabricAsync(installPath, minecraftVersion, modloaderVersion, javaArgs, startServer);
						break;
					case MinecraftModloader.Forge:
						await InstallForgeAsync(installPath, javaArgs, startServer);
						break;
					case MinecraftModloader.Unknown:
						Console.WriteLine("Error: Could not determine modloader, bailing out");
						return -1;
				}
			}

			return 0;
		}

		private static void GetCfApiInformation(out string cfApiKey, out long cfPartnerId, out string cfContactEmail)
		{
			cfApiKey = Environment.GetEnvironmentVariable("CFAPI_Key");
			_ = long.TryParse(Environment.GetEnvironmentVariable("CFAPI_PartnerId"), out cfPartnerId);
			cfContactEmail = Environment.GetEnvironmentVariable("CFAPI_ContactEmail");
			List<string> errors = new();

			if (string.IsNullOrWhiteSpace(cfApiKey))
			{
				errors.Add("Error: Missing CurseForge API key in environment variable CFAPI_Key");
			}

			if (cfPartnerId == 0)
			{
				errors.Add("Error: Missing CurseForge Partner Id in environment variable CFAPI_PartnerId");
			}

			if (string.IsNullOrWhiteSpace(cfContactEmail) || !MailAddress.TryCreate(cfContactEmail, out _))
			{
				errors.Add("Error: Missing contact email for the API key in environment variable CFAPI_ContactEmail");
			}

			if (errors.Count > 0)
			{
				errors.ForEach(Console.WriteLine);
			}
		}

		private static bool CheckRequiredDependencies()
		{
			var javaExists = ExecutableExistsOnPath(GetJavaExecutable());

			if (!javaExists)
			{
				Console.WriteLine($"Error: {GetJavaExecutable()} missing in PATH. Please install Java before continuing.");
			}

			return javaExists;
		}

		private static bool ExecutableExistsOnPath(string processName)
		{
			var separatedPaths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
			foreach (var path in separatedPaths)
			{
				var dir = Path.Combine(path, processName);
				if (File.Exists(dir))
				{
					return true;
				}
			}

			return false;
		}

		private static async Task<ModLoaderInfo<T>> GetLoaderDependencies<T>(ApiClient _client, string minecraftVersion, string loaderVersion) where T : ModLoaderVersionInfo
		{
			var loaderVersionEndpoint = typeof(T).Name == "FabricModLoaderInfo" ? $"{loaderVersion}-{minecraftVersion}" : loaderVersion;
			var modloaderInfo = (await _client.GetSpecificMinecraftModloaderInfo(loaderVersionEndpoint)).Data;

			var mli = new ModLoaderInfo<T>
			{
				DownloadUrl = new Uri(modloaderInfo.DownloadUrl),
				Filename = modloaderInfo.Filename,
				GameVersionId = modloaderInfo.GameVersionId,
				Id = modloaderInfo.Id,
				InstallProfileJson = modloaderInfo.InstallProfileJson,
				LibrariesInstallLocation = modloaderInfo.LibrariesInstallLocation,
				MinecraftGameVersionId = modloaderInfo.MinecraftGameVersionId,
				Name = modloaderInfo.Name,
				Type = (int)modloaderInfo.Type,
				VersionInfo = JsonSerializer.Deserialize<T>(modloaderInfo.VersionJson.ToString()),
				NonMapped = new Dictionary<string, object>
				{
					{ "minecraftVersion", modloaderInfo.MinecraftVersion },
					{ "forgeVersion", modloaderInfo.ForgeVersion }
				}
			};

			return mli;
		}

		private static async Task DownloadLoaderDependencies<T>(HttpClient _client, string installPath, ModLoaderInfo<T> info)
		{
			Console.WriteLine($"Downloading dependencies for {info.Name} (Minecraft version: {info.NonMapped["minecraftVersion"]})");

			var downloadUrls = new List<LibraryDownloadItem>();
			switch (info.VersionInfo)
			{
				case FabricModLoaderInfo fabric:
					var fabricInstallers = await _client.GetFromJsonAsync<List<FabricInstaller>>(FabricInstallerUrl);

					var latestInstaller = fabricInstallers.FirstOrDefault(i => i.Stable);
					downloadUrls.Add(new(latestInstaller.Maven, installPath, latestInstaller.Url));
					break;
				case ForgeModLoaderInfo forge:
					var versionString = $"{info.NonMapped["minecraftVersion"]}-{info.NonMapped["forgeVersion"]}";
					var forgeInstallerMaven = new MavenString($"net.minecraftforge.forge:{versionString}");
					downloadUrls.Add(new(forgeInstallerMaven, installPath, new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{versionString}/forge-{versionString}-installer.jar")));
					break;
			}

			using WebClient wc = new();
			foreach (var asset in downloadUrls)
			{
				var installDir = Path.Combine(installPath, "libraries", asset.FilePath);
				Directory.CreateDirectory(installDir);

				var fileDownloadPath = Path.Combine(installDir, asset.Url.Segments.Last());
				if (!File.Exists(fileDownloadPath))
				{
					Console.WriteLine($"Downloading: {asset.Url.Segments.Last()}");
					await wc.DownloadFileTaskAsync(asset.Url, fileDownloadPath);
				}
			}

			var loaderFile = Path.Combine(installPath, info.Filename);

			if (!File.Exists(loaderFile))
			{
				await wc.DownloadFileTaskAsync(info.DownloadUrl, loaderFile);
			}

			Console.WriteLine("Downloaded all library assets");
		}

		private static async Task DownloadJREAsync(HttpClient hc, string installPath, MinecraftVersionInfo.JavaVersionInfo javaVersion)
		{
			Console.WriteLine($"Downloading JRE {javaVersion.MajorVersion} ({javaVersion.Component})");
			var javaManifest = await hc.GetFromJsonAsync<Dictionary<string, Dictionary<string, List<MinecraftJavaInfo>>>>(MinecraftJavaManifestUrl);
			List<MinecraftJavaInfo> javaItems = null;

			if (OperatingSystem.IsLinux())
			{
				if (Environment.Is64BitOperatingSystem)
				{
					javaItems = javaManifest["linux"][javaVersion.Component];
				}
				else
				{
					javaItems = javaManifest["linux-i386"][javaVersion.Component];
				}
			}
			else if (OperatingSystem.IsMacOS())
			{
				javaItems = javaManifest["mac-os"][javaVersion.Component];
			}
			else if (OperatingSystem.IsWindows())
			{
				if (Environment.Is64BitOperatingSystem)
				{
					javaItems = javaManifest["windows-x64"][javaVersion.Component];
				}
				else
				{
					javaItems = javaManifest["windows-x86"][javaVersion.Component];
				}
			}

			if (javaItems == null)
			{
				throw new Exception("Could not find a suitable JRE to download");
			}

			var java = javaItems.First();

			Console.WriteLine($"Java JRE version {java.Version.Name}");

			var javaVersionManifest = await hc.GetFromJsonAsync<JavaInstallationClass>(java.Manifest.Url);

			Console.WriteLine("Downloading now");

			using WebClient wc = new();
			foreach (var javaFile in javaVersionManifest.Files.Where(f => f.Value.Type == "file"))
			{
				var javaFilePath = Path.Combine(installPath, "runtime", javaFile.Key);
				if (!File.Exists(javaFilePath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(javaFilePath));

					await wc.DownloadFileTaskAsync(javaFile.Value.Downloads["raw"].Url, javaFilePath);
					Console.WriteLine($"Downloaded (JRE): {javaFile.Key}");
				}
			}

			Console.WriteLine("JRE downloaded, continuing installation");
		}

		private static string GetJavaExecutable() => OperatingSystem.IsWindows() ? "java.exe" : "java";

		private static async Task InstallFabricAsync(string installPath, string minecraftVersion, string loaderVersion, string javaArgs, bool startServer)
		{
			var fabricInstaller = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("fabric-installer-"));
			if (fabricInstaller == null)
			{
				throw new Exception("Couldn't find the installer, bailing out");
			}

			var arguments = new[] {
				$"-jar {fabricInstaller}",
				"server",
				$"-mcversion {minecraftVersion}",
				$"-loader {loaderVersion}",
				"-downloadMinecraft"
			};

			await RunProcessAsync(installPath, GetJavaExecutable(), false, arguments);

			if (startServer)
			{
				await RunProcessAsync(installPath, Path.Combine(installPath, "runtime", "bin", GetJavaExecutable()), true, javaArgs, "-Dsun.stdout.encoding=UTF-8", $"-jar fabric-server-launch.jar nogui");
			}
			else
			{
				AnsiConsole.Write(new Markup($"To start the server, you can write [orange1 bold]{Path.Combine(installPath, "runtime", "bin", GetJavaExecutable())} {javaArgs} -Dsun.stdout.encoding=UTF-8 -jar fabric-server-launch.jar nogui[/]"));
			}
		}

		private static async Task InstallForgeAsync(string installPath, string javaArgs, bool startServer)
		{
			var forgeInstaller = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("forge-") && f.Contains("-installer.jar"));
			if (forgeInstaller == null)
			{
				throw new Exception("Couldn't find the installer, bailing out");
			}

			var arguments = new[] {
				$"-jar {forgeInstaller}",
				"--installServer"
			};

			await RunProcessAsync(installPath, GetJavaExecutable(), false, arguments);

			var forgeLoader = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("forge-") && !f.Contains("-installer.jar"));

			if (forgeLoader == null)
			{
				Console.WriteLine("Could not find the loader, please launch server manually");
			}

			if (startServer)
			{
				await RunProcessAsync(installPath, Path.Combine(installPath, "runtime", "bin", GetJavaExecutable()), true, javaArgs, "-Dsun.stdout.encoding=UTF-8", $"-jar {forgeLoader} nogui");
			}
			else
			{
				AnsiConsole.Write(new Markup($"To start the server, you can write [orange1 bold]{Path.Combine(installPath, "runtime", "bin", GetJavaExecutable())} {javaArgs} -Dsun.stdout.encoding=UTF-8 -jar {forgeLoader} nogui[/]"));
			}
		}

		private static async Task RunProcessAsync(string executingDirectory, string process, bool redirectInput, params string[] arguments)
		{
			Console.WriteLine($"Executing \"{process}\" in \"{executingDirectory}\", with arguments: \"{string.Join(" ", arguments)}\"");
			using var p = new Process();
			p.StartInfo = new ProcessStartInfo(process, string.Join(" ", arguments))
			{
				WorkingDirectory = executingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = !redirectInput
			};

			p.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
			p.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

			p.Start();

			p.BeginErrorReadLine();
			p.BeginOutputReadLine();

			await p.WaitForExitAsync();
		}

	}
}
