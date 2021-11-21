using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class ModLoaderInfo<T>
	{
		[JsonPropertyName("id")]
		public long Id { get; set; }
		[JsonPropertyName("gameVersionId")]
		public long GameVersionId { get; set; }
		[JsonPropertyName("minecraftGameVersionId")]
		public long MinecraftGameVersionId { get; set; }
		[JsonPropertyName("name")]
		public string Name { get; set; }
		[JsonPropertyName("type")]
		public int Type { get; set; }
		[JsonPropertyName("downloadUrl")]
		public Uri DownloadUrl { get; set; }
		[JsonPropertyName("filename")]
		public string Filename { get; set; }
		[JsonExtensionData]
		public Dictionary<string, object> NonMapped { get; set; }
		public T VersionInfo { get; set; }
		[JsonPropertyName("librariesInstallLocation")]
		public string LibrariesInstallLocation { get; set; }
		[JsonPropertyName("installProfileJson")]
		public string InstallProfileJson { get; set; }
	}

}
