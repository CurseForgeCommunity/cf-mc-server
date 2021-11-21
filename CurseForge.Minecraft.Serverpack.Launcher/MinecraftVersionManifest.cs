using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MinecraftVersionManifest
	{
		[JsonPropertyName("latest")]
		public LatestVersions Latest { get; set; }

		public class LatestVersions
		{
			[JsonPropertyName("release")]
			public string Release { get; set; }
			[JsonPropertyName("snapshot")]
			public string Snapshot { get; set; }
		}

		[JsonPropertyName("versions")]
		public List<MinecraftVersion> Versions { get; set; } = new List<MinecraftVersion>();

		public class MinecraftVersion
		{
			[JsonPropertyName("id")]
			public string Id { get; set; }
			[JsonPropertyName("type")]
			public string Type { get; set; }
			[JsonPropertyName("url")]
			public Uri Url { get; set; }
			[JsonPropertyName("time")]
			public DateTimeOffset Time { get; set; }
			[JsonPropertyName("releaseTime")]
			public DateTimeOffset ReleaseTime { get; set; }
		}
	}
}
