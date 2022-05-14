using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static async Task InstallForgeAsync(string installPath, string javaArgs, bool startServer)
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

			await RunProcessAsync(installPath, GetJavaExecutable(), false, arguments);

			var forgeLoader = Directory.EnumerateFiles(installPath, "*.jar", SearchOption.AllDirectories).FirstOrDefault(f => f.Contains("forge-") && !f.Contains("-installer.jar") && f.EndsWith(".jar"));

			if (forgeLoader == null)
			{
				Console.WriteLine("Could not find the loader, please launch server manually");
			}

			var javaPath = Path.Combine(installPath, "runtime", "bin", GetJavaExecutable());

			CreateLaunchScriptIfMissing(installPath, javaPath, javaArgs, forgeLoader);
		}
	}
}
