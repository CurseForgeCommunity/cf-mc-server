using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class ModLoaderVersionInfo
	{
		[JsonPropertyName("id")]
		public string Id { get; set; }
		[JsonPropertyName("inheritsFrom")]
		public string InheritsFrom { get; set; }
		[JsonPropertyName("mainClass")]
		public string MainClass { get; set; }
		[JsonPropertyName("arguments")]
		public LoaderArgument Arguments { get; set; }
	}

}
