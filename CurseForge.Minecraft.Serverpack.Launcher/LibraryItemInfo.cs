using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class LibraryItemInfo
	{
		[JsonPropertyName("name")]
		public string Name { get; set; }
		[JsonPropertyName("downloads")]
		public Download Downloads { get; set; }
		[JsonPropertyName("natives")]
		public Dictionary<string, string> Natives { get; set; }
		[JsonPropertyName("rules")]
		public List<LibraryItemRule> Rules { get; set; } = new List<LibraryItemRule>();
		[JsonExtensionData]
		public Dictionary<string, object> NonMapped { get; set; }

		public List<LibraryDownloadItem> GetDownloadItems()
		{
			var downloadItems = new List<LibraryDownloadItem>();
			if (Rules?.Count > 0)
			{
				var allowRules = Rules.Where(r => r.Action == "allow").ToList();
				var denyRules = Rules.Where(r => r.Action == "disallow").ToList();

				if (denyRules.Count > 0)
				{
					foreach (var deny in denyRules)
					{
						if (deny.OS == null)
							return null;

						if (deny.OS.Name == "osx" && OperatingSystem.IsMacOS())
						{
							return null;
						}

						if (deny.OS.Name == "windows" && OperatingSystem.IsWindows())
						{
							return null;
						}

						if (deny.OS.Name == "linux" && OperatingSystem.IsLinux())
						{
							return null;
						}
					}
				}

				if (allowRules.Count > 0)
				{
					var allowedDownload = false;
					foreach (var allow in allowRules)
					{
						if (allow.OS == null)
						{
							allowedDownload = true;
							break;
						}

						if (allow.OS.Name == "osx" && OperatingSystem.IsMacOS())
						{
							allowedDownload = true;
							break;
						}

						if (allow.OS.Name == "windows" && OperatingSystem.IsWindows())
						{
							allowedDownload = true;
							break;
						}

						if (allow.OS.Name == "linux" && OperatingSystem.IsLinux())
						{
							allowedDownload = true;
							break;
						}
					}

					if (!allowedDownload)
					{
						return null;
					}
				}
			}

			if (Downloads.Artifact != null)
			{
				var dlUrl = new Uri(Downloads.Artifact.Url);
				downloadItems.Add(new LibraryDownloadItem(Name, Downloads.Artifact.Path.Replace(dlUrl.Segments.Last(), ""), new Uri(Downloads.Artifact.Url)));
			}

			if (Downloads.Classifiers.Count > 0)
			{
				if (OperatingSystem.IsMacOS() && Downloads.Classifiers.Any(c => c.Key == "natives-macos"))
				{
					var mac = Downloads.Classifiers.First(c => c.Key == "natives-macos").Value;
					var macDlUrl = new Uri(mac.Url);
					downloadItems.Add(new(Name, mac.Path.Replace(macDlUrl.Segments.Last(), ""), new Uri(mac.Url)));
				}

				if (OperatingSystem.IsLinux() && Downloads.Classifiers.Any(c => c.Key == "natives-linux"))
				{
					var linux = Downloads.Classifiers.First(c => c.Key == "natives-linux").Value;
					var linuxDlUrl = new Uri(linux.Url);
					downloadItems.Add(new(Name, linux.Path.Replace(linuxDlUrl.Segments.Last(), ""), new Uri(linux.Url)));
				}

				if (OperatingSystem.IsWindows() && Downloads.Classifiers.Any(c => c.Key == "natives-windows"))
				{
					var windows = Downloads.Classifiers.First(c => c.Key == "natives-windows").Value;
					var windowsDlUrl = new Uri(windows.Url);
					downloadItems.Add(new(Name, windows.Path.Replace(windowsDlUrl.Segments.Last(), ""), new Uri(windows.Url)));
				}
			}

			return downloadItems;
		}
	}

}
