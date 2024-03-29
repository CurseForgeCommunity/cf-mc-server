﻿using System;
using System.Diagnostics;
using System.IO;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static void CreateSpecialLaunchScriptIfMissing(string installPath, string javaPath, string javaArgs, params string[] arguments)
		{
			if (OperatingSystem.IsWindows())
			{
				var launchScript = Path.Combine(installPath, "start-server.bat");

				if (!File.Exists(launchScript))
				{
					File.WriteAllText(launchScript, $@"@echo OFF
cd {installPath}
{javaPath} {javaArgs} -Dsun.stdout.encoding=UTF-8 {string.Join(" ", arguments)} nogui
echo Server has stopped, press any key to continue
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
{javaPath} {javaArgs} -Dsun.stdout.encoding=UTF-8 {string.Join(" ", arguments)} nogui");

					var p = new Process()
					{
						StartInfo = new ProcessStartInfo()
						{
							FileName = "/bin/chmod",
							Arguments = $"+x {launchScript}",
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							RedirectStandardInput = true
						}
					};

					p.Start();
					p.StandardOutput.ReadToEnd();
					p.WaitForExit();

				}
			}
		}

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
echo Server has stopped, press any key to continue
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
							FileName = "/bin/chmod",
							Arguments = $"+x {launchScript}",
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							RedirectStandardInput = true
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
