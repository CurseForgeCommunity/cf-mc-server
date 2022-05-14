using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CurseForge.APIClient;
using Spectre.Console;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static bool CheckRequiredDependencies()
		{
			var javaExists = ExecutableExistsOnPath(GetJavaExecutable());

			if (!javaExists)
			{
				Console.WriteLine($"Error: {GetJavaExecutable()} missing in PATH. Please install Java before continuing.");
			}

			return javaExists;
		}

		private static async Task DownloadMinecraftLibraries(ApiClient cfApiClient, string installPath, MinecraftManifest manifest, MinecraftVersionInfo mcVersionInfo)
		{
			var downloadItems = new List<LibraryDownloadItem>();

			foreach (var lib in mcVersionInfo.Libraries)
			{
				downloadItems.AddRange(lib.GetDownloadItems() ?? new());
			}

#pragma warning disable SYSLIB0014 // Type or member is obsolete
			using WebClient wc = new();
#pragma warning restore SYSLIB0014 // Type or member is obsolete
			// TODO: Change into using Spectre.Console for better progress
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
			// TODO: Change into using Spectre.Console for better progress
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

					var forgeDlUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{versionString}/forge-{versionString}-installer.jar";

					if (!await CheckIfEndpointExists(_client, forgeDlUrl))
					{
						versionString = $"{info.NonMapped["minecraftVersion"]}-{info.NonMapped["forgeVersion"]}-{info.NonMapped["minecraftVersion"]}";
						forgeDlUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{versionString}/forge-{versionString}-installer.jar";
					}

					if (!await CheckIfEndpointExists(_client, forgeDlUrl))
					{
						AnsiConsole.WriteLine($"[red]Could not find an installer for the version of Forge that we need ({info.NonMapped["forgeVersion"]}) for Minecraft {info.NonMapped["minecraftVersion"]}[/]");
						throw new Exception("Missing Forge installer");
					}

					downloadUrls.Add(new(forgeInstallerMaven, installPath, new Uri(forgeDlUrl)));
					break;
			}

#pragma warning disable SYSLIB0014 // Type or member is obsolete
			using WebClient wc = new();
#pragma warning restore SYSLIB0014 // Type or member is obsolete
			// TODO: Change into using Spectre.Console for better progress
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

		private static async Task<bool> CheckIfEndpointExists(HttpClient _client, string url)
		{
			var result = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
			return result.IsSuccessStatusCode;
		}
	}
}
