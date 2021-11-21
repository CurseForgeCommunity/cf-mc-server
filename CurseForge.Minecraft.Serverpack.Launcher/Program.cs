using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		const string MinecraftVersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
		const string MinecraftJavaManifestUrl = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

		const string FabricInstallerUrl = "https://meta.fabricmc.net/v2/versions/installer";

		static async Task<int> Main(string[] args)
		{
			if (args.Length < 3)
			{
				Console.WriteLine("Too few arguments, needs a project ID for a modpack (Minecraft) that has server files and a path where to install the server");
				Console.WriteLine("cf-mc-server [projectId] [fileId] [server install path]");
				Console.WriteLine();
				Console.WriteLine("Examples:");
				Console.WriteLine("cf-mc-server 477455 3295539 \"c:\\mc-server\"");
				Console.WriteLine("-- Installs the modpack \"Too Many Projects\", into the \"mc-server\" folder in the C-drive");
				Console.WriteLine();
				Console.WriteLine("Don't use the root of a drive to install things.");
				return -1;
			}

			if (!int.TryParse(args[0], out int modId))
			{
				Console.WriteLine("First parameter is not a project id, use only numbers");
				return -1;
			}

			Console.WriteLine($"Using project: {modId}");

			if (!int.TryParse(args[1], out int fileId))
			{
				Console.WriteLine("Second parameter is not a file id, use only numbers");
				return -1;
			}

			Console.WriteLine($"Using file: {fileId}");

			string path = args[2];

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

			var cpApiKey = Environment.GetEnvironmentVariable("CFAPI_Key");
			_ = long.TryParse(Environment.GetEnvironmentVariable("CFAPI_PartnerId"), out long cfPartnerId);
			var cpContactEmail = Environment.GetEnvironmentVariable("CFAPI_ContactEmail");

			using (var cfApiClient = new APIClient.ApiClient(cpApiKey, cfPartnerId, cpContactEmail))
			{
				var modLoader = MinecraftModloader.Unknown;
				var modInfo = await cfApiClient.GetModAsync(modId);

				Console.WriteLine($"Getting information about {modInfo.Data.Name}");

				if (!modInfo.Data.Categories.Any(c => c.ClassId == 4471))
				{
					// Not a modpack
					Console.WriteLine($"Project is not a modpack, not allowed in current version of server launcher");
					return -1;
				}

				var modFile = await cfApiClient.GetModFileAsync(modId, fileId);

				var dlPath = Path.Combine(path, modFile.Data.FileName);
				var installPath = Path.Combine(path, "installed", modInfo.Data.Slug);
				var manifestPath = Path.Combine(installPath, "manifest.json");

				if (!File.Exists(dlPath))
				{
					using (var wc = new WebClient())
					{
						await wc.DownloadFileTaskAsync(modFile.Data.DownloadUrl, dlPath);
					}
				}

				if (!Directory.Exists(installPath))
				{
					ZipFile.ExtractToDirectory(dlPath, installPath, true);
				}

				var manifest = JsonSerializer.Deserialize<MinecraftManifest>(File.ReadAllText(manifestPath));

				using (var hc = new HttpClient())
				{
					var gameVersionManifest = JsonSerializer.Deserialize<MinecraftVersionManifest>(await hc.GetStringAsync(MinecraftVersionManifestUrl));
					var mcVersion = gameVersionManifest.Versions.FirstOrDefault(version => version.Id == manifest.Minecraft.Version);

					if (mcVersion == null)
					{
						Console.WriteLine($"Could not find version {manifest.Minecraft.Version}, exiting");
						return -1;
					}

					//mcVersion.Dump("Manifest version");
					var mcVersionInfo = JsonSerializer.Deserialize<MinecraftVersionInfo>(await hc.GetStringAsync(mcVersion.Url));

					//mcVersionInfo.Dump();
					await DownloadJREAsync(hc, installPath, mcVersionInfo.JavaVersion);

					string modloaderVersion = string.Empty;
					string minecraftVersion = string.Empty;

					if (manifest.Minecraft.ModLoaders.Any(ml => ml.Id.StartsWith("fabric-") && ml.Primary))
					{
						foreach (var modloader in manifest.Minecraft.ModLoaders.Where(ml => ml.Id.StartsWith("fabric-") && ml.Primary))
						{
							var modloaderInfo = await GetLoaderDependencies<FabricModLoaderInfo>(hc, manifest.Minecraft.Version, modloader.Id);
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
							var modloaderInfo = await GetLoaderDependencies<ForgeModLoaderInfo>(hc, manifest.Minecraft.Version, modloader.Id);
							await DownloadLoaderDependencies(hc, installPath, modloaderInfo);
							modloaderVersion = modloaderInfo.NonMapped["forgeVersion"].ToString();
							minecraftVersion = modloaderInfo.NonMapped["minecraftVersion"].ToString();
						}

						modLoader = MinecraftModloader.Forge;
					}

					if (modLoader == MinecraftModloader.Unknown)
					{
						Console.WriteLine("Could not determine modloader, bailing out");
						return -1;
					}

					var downloadItems = new List<LibraryDownloadItem>();

					foreach (var lib in mcVersionInfo.Libraries)
					{
						downloadItems.AddRange(lib.GetDownloadItems() ?? new());
					}

					using (var wc = new WebClient())
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
							await InstallFabricAsync(installPath, minecraftVersion, modloaderVersion);
							break;
						case MinecraftModloader.Forge:
							await InstallForgeAsync(installPath);
							break;
						case MinecraftModloader.Unknown:
							Console.WriteLine("Could not determine modloader, bailing out");
							return -1;
					}
				}
			}

			return 0;
		}

		private static async Task<ModLoaderInfo<T>> GetLoaderDependencies<T>(HttpClient _client, string minecraftVersion, string loaderVersion) where T : ModLoaderVersionInfo
		{
			var loaderVersionEndpoint = typeof(T).Name == "FabricModLoaderInfo" ? $"{loaderVersion}-{minecraftVersion}" : loaderVersion;

			var modloaderInfoJson = await _client.GetStringAsync($"https://addons-ecs.forgesvc.net/api/v2/minecraft/modloader/{loaderVersionEndpoint}");
			var modloaderInfo = JsonSerializer.Deserialize<ModLoaderInfo<T>>(modloaderInfoJson);

			modloaderInfo.VersionInfo = JsonSerializer.Deserialize<T>(modloaderInfo.NonMapped["versionJson"].ToString());

			return modloaderInfo;
		}

		private static async Task DownloadLoaderDependencies<T>(HttpClient _client, string installPath, ModLoaderInfo<T> info)
		{
			Console.WriteLine($"Downloading dependencies for {info.Name} (Minecraft version: {info.NonMapped["minecraftVersion"]})");

			var downloadUrls = new List<LibraryDownloadItem>();

			switch (info.VersionInfo)
			{
				case FabricModLoaderInfo fabric:
					/*foreach (var library in fabric.Libraries)
					{
						var dlUrl = new Uri(library.MavenInfo.GetDownloadUrl(library.Url));
						downloadUrls.Add(new(library.Name, dlUrl.PathAndQuery.Replace(dlUrl.Segments.Last(), ""), dlUrl));
					}*/

					var fabricInstallers = JsonSerializer.Deserialize<List<FabricInstaller>>(await _client.GetStringAsync(FabricInstallerUrl));

					var latestInstaller = fabricInstallers.FirstOrDefault(i => i.Stable);
					downloadUrls.Add(new(latestInstaller.Maven, installPath, latestInstaller.Url));
					break;
				case ForgeModLoaderInfo forge:
					/*foreach (var library in forge.Libraries)
					{
						var dlUrl = new Uri(library.Downloads.Artifact.Url);
						downloadUrls.Add(new(library.Name, library.Downloads.Artifact.Path.Replace(dlUrl.Segments.Last(), ""), dlUrl));
					}*/

					var versionString = $"{info.NonMapped["minecraftVersion"]}-{info.NonMapped["forgeVersion"]}";
					var forgeInstallerMaven = new MavenString($"net.minecraftforge.forge:{versionString}");
					downloadUrls.Add(new(forgeInstallerMaven, installPath, new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{versionString}/forge-{versionString}-installer.jar")));
					break;
			}

			using (var wc = new WebClient())
			{
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
		}

		private static async Task DownloadJREAsync(HttpClient hc, string installPath, MinecraftVersionInfo.JavaVersionInfo javaVersion)
		{
			Console.WriteLine($"Downloading JRE {javaVersion.MajorVersion} ({javaVersion.Component})");
			var javaManifest = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<MinecraftJavaInfo>>>>(await hc.GetStringAsync(MinecraftJavaManifestUrl));
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

			var javaVersionManifest = JsonSerializer.Deserialize<JavaInstallationClass>(await hc.GetStringAsync(java.Manifest.Url));

			Console.WriteLine("Downloading now");

			using (var wc = new WebClient())
			{
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
			}

			Console.WriteLine("JRE downloaded, continuing installation");
		}

		private static async Task InstallFabricAsync(string installPath, string minecraftVersion, string loaderVersion)
		{
			var fabricInstaller = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("fabric-installer-"));
			if (fabricInstaller == null)
			{
				throw new Exception("Couldn't find the installer, bailing out");
			}

			var arguments = new[] { $"-jar {fabricInstaller}", "server", $"-mcversion {minecraftVersion}", $"-loader {loaderVersion}", "-downloadMinecraft" };

			if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
			{
				await RunProcessAsync(installPath, "java", arguments);
			}
			else if (OperatingSystem.IsWindows())
			{
				await RunProcessAsync(installPath, "java.exe", arguments);
			}
		}

		private static async Task InstallForgeAsync(string installPath)
		{
			var forgeInstaller = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("forge-") && f.Contains("-installer.jar"));
			if (forgeInstaller == null)
			{
				throw new Exception("Couldn't find the installer, bailing out");
			}

			var arguments = new[] { $"-jar {forgeInstaller}", "--installServer" };

			if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
			{
				await RunProcessAsync(installPath, "java", arguments);
			}
			else if (OperatingSystem.IsWindows())
			{
				await RunProcessAsync(installPath, "java.exe", arguments);
			}
		}

		private static async Task RunProcessAsync(string executingDirectory, string process, params string[] arguments)
		{
			Console.WriteLine($"Executing \"{process}\" in \"{executingDirectory}\", with arguments: \"{string.Join(" ", arguments)}\"");
			using (var p = new Process())
			{
				p.StartInfo = new ProcessStartInfo(process, string.Join(" ", arguments))
				{
					WorkingDirectory = executingDirectory,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
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
}
