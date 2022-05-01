using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
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

			var javaPath = Path.Combine(installPath, "runtime", "bin", GetJavaExecutable());

			CreateLaunchScriptIfMissing(installPath, javaPath, javaArgs, "fabric-server-launch.jar");

			if (startServer)
			{
				await RunProcessAsync(installPath, javaPath, true, javaArgs, "-Dsun.stdout.encoding=UTF-8", $"-jar fabric-server-launch.jar nogui");
			}
			else
			{
				AnsiConsole.Write(new Markup($"To start the server, you can write [orange1 bold]{(OperatingSystem.IsWindows() ? "start-server.bat" : "./start-server.sh")}[/]"));
			}
		}
	}
}
