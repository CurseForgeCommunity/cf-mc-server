using System;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class FabricInstaller
	{
		[JsonPropertyName("url")]
		public Uri Url { get; set; }
		[JsonPropertyName("maven")]
		public string Maven { get; set; }
		[JsonPropertyName("version")]
		public string Version { get; set; }
		[JsonPropertyName("stable")]
		public bool Stable { get; set; }
	}

}
