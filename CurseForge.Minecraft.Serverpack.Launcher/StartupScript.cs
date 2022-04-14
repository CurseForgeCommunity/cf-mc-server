using System;
using System.Diagnostics;
using System.IO;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static void CreateLaunchScriptIfMissing(string installPath, string javaPath, string javaArgs, string jarFile)
		{
			if (OperatingSystem.IsWindows())
			{
				var launchScript = Path.Combine(installPath, "start-server.bat");

				if (!File.Exists(launchScript))
				{
					File.WriteAllText(launchScript, $@"@echo OFF
cd {installPath}
{javaPath} {javaArgs} -Dsun.stdout.encoding=UTF-8 -jar {jarFile} nogui
pause");
				}
			}
			else
			{
				var launchScript = Path.Combine(installPath, "start-server.sh");

				if (!File.Exists(launchScript))
				{
					File.WriteAllText(launchScript, $@"#!/bin/sh
cd {installPath}
{javaPath} {javaArgs} -Dsun.stdout.encoding=UTF-8 -jar {jarFile} nogui");

					var p = new Process()
					{
						StartInfo = new ProcessStartInfo()
						{
							FileName = "chmod",
							Arguments = $"+x {launchScript}",
							UseShellExecute = false,
							CreateNoWindow = true
						}
					};

					p.Start();
					p.StandardOutput.ReadToEnd();
					p.WaitForExit();
				}
			}
		}
	}
}
