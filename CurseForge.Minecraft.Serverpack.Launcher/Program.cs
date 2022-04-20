using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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

		private static Mod selectedMod = null;
		private static APIClient.Models.Files.File selectedVersion = null;

		internal const string CFApiKey = "--REPLACEME--";

		private static async Task<int> Main(params string[] args)
		{
			var command = SetupCommand();
			if (args.Length == 0)
			{
				await command.InvokeAsync("--help");
				Console.ReadKey();

				return 0;
			}

			return await command.InvokeAsync(args);
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

		private static async Task<int> InstallServer(int modId, int fileId, string path, string javaArgs, bool startServer)
		{
			GetCfApiInformation(out var cfApiKey, out var cfPartnerId, out var cfContactEmail, out var errors);

			if (errors.Count > 0)
			{
				AnsiConsole.WriteLine("[bold red]Please resolve the errors before continuing.[/]");
				return -1;
			}

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
					AnsiConsole.WriteLine("[bold red]Error: Project is not a modpack, not allowed in current version of server launcher[/]");
					return -1;
				}

				if (!(modInfo.Data.AllowModDistribution ?? true) || !modInfo.Data.IsAvailable)
				{
					AnsiConsole.WriteLine("[bold red]The author of this modpack has not made it available for download through third party tools.[/]");
					return -1;
				}

				var modFile = await cfApiClient.GetModFileAsync(modId, fileId);

				var dlPath = Path.Combine(path, modFile.Data.FileName);
				var installPath = Path.Combine(path, "installed", modInfo.Data.Slug);
				var manifestPath = Path.Combine(installPath, "manifest.json");

				if (!File.Exists(dlPath))
				{
#pragma warning disable SYSLIB0014 // Type or member is obsolete
					using WebClient wc = new();
#pragma warning restore SYSLIB0014 // Type or member is obsolete
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

				await DownloadMinecraftLibraries(cfApiClient, installPath, manifest, mcVersionInfo);

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

		private static string GetJavaExecutable() => OperatingSystem.IsWindows() ? "java.exe" : "java";

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
