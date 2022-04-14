using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
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

			var javaPath = Path.Combine(installPath, "runtime", "bin", GetJavaExecutable());

			CreateLaunchScriptIfMissing(installPath, javaPath, javaArgs, forgeLoader);

			if (startServer)
			{
				await RunProcessAsync(installPath, javaPath, true, javaArgs, "-Dsun.stdout.encoding=UTF-8", $"-jar {forgeLoader} nogui");
			}
			else
			{
				AnsiConsole.Write(new Markup($"To start the server, you can write [orange1 bold]{javaPath} {javaArgs} -Dsun.stdout.encoding=UTF-8 -jar {forgeLoader} nogui[/]"));
			}
		}
	}
}
