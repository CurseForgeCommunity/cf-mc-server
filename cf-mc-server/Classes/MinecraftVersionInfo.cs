using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MinecraftVersionInfo
	{
		[JsonPropertyName("assets")]
		public string Assets { get; set; }
		[JsonPropertyName("complianceLevel")]
		public long ComplianceLevel { get; set; }
		[JsonPropertyName("id")]
		public string Id { get; set; }
		[JsonPropertyName("mainClass")]
		public string MainClass { get; set; }
		[JsonPropertyName("minimumLauncherVersion")]
		public long MinimumLauncherVersion { get; set; }
		[JsonPropertyName("releaseTime")]
		public DateTimeOffset ReleaseTime { get; set; }
		[JsonPropertyName("time")]
		public DateTimeOffset Time { get; set; }
		[JsonPropertyName("type")]
		public string Type { get; set; }

		[JsonPropertyName("javaVersion")]
		public JavaVersionInfo JavaVersion { get; set; }

		public class JavaVersionInfo
		{
			[JsonPropertyName("component")]
			public string Component { get; set; }
			[JsonPropertyName("majorVersion")]
			public long MajorVersion { get; set; }
		}

		[JsonPropertyName("downloads")]
		public MinecraftDownloadItems Downloads { get; set; }

		public class MinecraftDownloadItems
		{
			[JsonPropertyName("client")]
			public MinecraftDownloadItem Client { get; set; }
			[JsonPropertyName("client_mappings")]
			public MinecraftDownloadItem ClientMappings { get; set; }
			[JsonPropertyName("server")]
			public MinecraftDownloadItem Server { get; set; }
			[JsonPropertyName("server_mappings")]
			public MinecraftDownloadItem ServerMappings { get; set; }

			public class MinecraftDownloadItem
			{
				[JsonPropertyName("sha1")]
				public string SHA1 { get; set; }
				[JsonPropertyName("url")]
				public Uri Url { get; set; }
				[JsonPropertyName("size")]
				public long Size { get; set; }
			}
		}

		[JsonPropertyName("libraries")]
		public List<LibraryItemInfo> Libraries { get; set; } = new List<LibraryItemInfo>();
	}
}
