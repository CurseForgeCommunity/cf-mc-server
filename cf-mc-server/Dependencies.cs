﻿using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

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

			var manualInstall = new List<(Mod mod, APIClient.Models.Files.File file)>();

			foreach (var file in manifest.Files)
			{
				var mod = await cfApiClient.GetModFileAsync(file.ProjectId, file.FileId);

				if (mod.Data.GameVersions.Contains("Client") && !mod.Data.GameVersions.Contains("Server"))
				{
					AnsiConsole.MarkupLineInterpolated($"[darkorange]The file {mod.Data.DisplayName} is marked as client only, and will not be installed, as it might break the server[/]");
					continue;
				}

				var modDlUrl = mod.Data.DownloadUrl;
				if (string.IsNullOrWhiteSpace(modDlUrl))
				{
					var modData = await cfApiClient.GetModAsync(file.ProjectId);
					manualInstall.Add((modData.Data, mod.Data));
					AnsiConsole.MarkupLineInterpolated($"[red]Could not find a download URL for the mod {modData.Data.Name} ({mod.Data.DisplayName}), needs a manual install[/]");
					continue;
				}

				var modPath = Path.Combine(installPath, "mods", mod.Data.FileName);
				Directory.CreateDirectory(Path.GetDirectoryName(modPath));
				if (!File.Exists(modPath))
				{
					Console.WriteLine($"Downloading (mods): {mod.Data.DisplayName} ({mod.Data.FileName})");
					await wc.DownloadFileTaskAsync(mod.Data.DownloadUrl, modPath);
				}
			}

			if (manualInstall.Count > 0)
			{
				AnsiConsole.MarkupLine("[red]The mods listed below needs to be downloaded manually, since they don't allow 3rd party clients to download the mods through the API[/]");
				AnsiConsole.WriteLine($"The mods needs to be downloaded into: {Path.Combine(installPath, "mods")}");
				Console.WriteLine();

				foreach (var (mod, file) in manualInstall)
				{
					Console.WriteLine($"File {file.FileName} needs to be downloaded from: {mod.Links.WebsiteUrl}/files/{file.Id}");
				}

				Console.WriteLine();
				Console.WriteLine($"Take a moment to download all these mods into \"{Path.Combine(installPath, "mods")}\" before continuing, then press Enter when you're done.");
				Console.ReadLine();

				foreach (var (_, file) in manualInstall)
				{
					var modPath = Path.Combine(installPath, "mods", file.FileName);
					if (!File.Exists(modPath))
					{
						Console.WriteLine($"File {file.FileName} not yet downloaded, don't blame me if the pack doesn't work as expected.");
						Console.ReadLine();
					}
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
						AnsiConsole.MarkupLineInterpolated($"[red]Could not find an installer for the version of Forge that we need ({info.NonMapped["forgeVersion"]}) for Minecraft {info.NonMapped["minecraftVersion"]}[/]");
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
