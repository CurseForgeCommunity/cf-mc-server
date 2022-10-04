using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static async Task InstallForgeAsync(string installPath, string minecraftVersion, string modloaderVersion, string javaArgs)
		{
			var forgeInstaller = Directory.EnumerateFiles(installPath).FirstOrDefault(f => f.Contains("forge-") && f.Contains("-installer.jar") && f.EndsWith(".jar"));
			if (forgeInstaller == null)
			{
				throw new Exception("Couldn't find the installer, bailing out");
			}

			var arguments = new[] {
				$"-jar {forgeInstaller}",
				"--installServer"
			};

			var mcVersion = new Version(minecraftVersion);

			var javaInstallPath = mcVersion.Minor <= 16 ? GetJavaExecutable() : Path.Combine(installPath, "runtime", "bin", GetJavaExecutable());
			var javaPath = Path.Combine(installPath, "runtime", "bin", GetJavaExecutable());

			await RunProcessAsync(installPath, javaInstallPath, false, arguments);

			var runFile = Path.Combine(installPath, OperatingSystem.IsWindows() ? "run.bat" : "run.sh");

			var forgeLoader = Directory.EnumerateFiles(installPath, "*.jar", SearchOption.AllDirectories).FirstOrDefault(f => f.Contains("forge-") && !f.Contains("-installer.jar") && f.EndsWith(".jar"));

			if (forgeLoader == null)
			{
				Console.WriteLine("Could not find the loader, please launch server manually");
			}

			// This is if Forge started using their new run-files, instead of putting the jar in the folder
			if (File.Exists(runFile))
			{
				var newForgePath = Path.Combine(installPath, "libraries", "net", "minecraftforge", "forge");
				var forgeVersion = Directory.EnumerateDirectories(newForgePath).FirstOrDefault();
				var configFile = Path.Combine(forgeVersion, OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt");
				CreateSpecialLaunchScriptIfMissing(installPath, javaPath, javaArgs, $"@{configFile}");
			}
			else
			{
				CreateLaunchScriptIfMissing(installPath, javaPath, javaArgs, forgeLoader);
			}
		}
	}
}
