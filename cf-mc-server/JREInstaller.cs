using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
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

#pragma warning disable SYSLIB0014 // Type or member is obsolete
			using WebClient wc = new();
#pragma warning restore SYSLIB0014 // Type or member is obsolete
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
	}
}
