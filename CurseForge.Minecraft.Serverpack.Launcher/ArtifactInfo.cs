using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class ArtifactInfo
	{
		[JsonPropertyName("path")]
		public string Path { get; set; }
		[JsonPropertyName("sha1")]
		public string SHA1 { get; set; }
		[JsonPropertyName("size")]
		public long Size { get; set; }
		[JsonPropertyName("url")]
		public string Url { get; set; }
	}

}
