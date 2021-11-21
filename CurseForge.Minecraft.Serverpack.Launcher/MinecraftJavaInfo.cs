using System;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MinecraftJavaInfo
	{
		[JsonPropertyName("availability")]
		public JavaAvailability Availability { get; set; }
		[JsonPropertyName("manifest")]
		public ArtifactInfo Manifest { get; set; }
		[JsonPropertyName("version")]
		public JavaVersion Version { get; set; }

		public class JavaAvailability
		{
			[JsonPropertyName("group")]
			public long Group { get; set; }
			[JsonPropertyName("progress")]
			public long Progress { get; set; }
		}

		public class JavaVersion
		{
			[JsonPropertyName("name")]
			public string Name { get; set; }
			[JsonPropertyName("released")]
			public DateTimeOffset Released { get; set; }
		}
	}
}
