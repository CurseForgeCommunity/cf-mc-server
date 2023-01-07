using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class CurseForgeManifest
	{
		[JsonPropertyName("manifestType")]
		public string ManifestType { get; set; }
		[JsonPropertyName("manifestVersion")]
		public long ManifestVersion { get; set; }
		[JsonPropertyName("name")]
		public string Name { get; set; }
		[JsonPropertyName("version")]
		public string Version { get; set; }
		[JsonPropertyName("author")]
		public string Author { get; set; }
		[JsonPropertyName("overrides")]
		public string Overrides { get; set; }
		[JsonPropertyName("files")]
		public List<CurseForgeFile> Files { get; set; } = new List<CurseForgeFile>();
	}

}
