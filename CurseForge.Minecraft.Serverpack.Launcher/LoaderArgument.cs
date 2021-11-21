using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class LoaderArgument
	{
		[JsonPropertyName("game")]
		public List<string> Game { get; set; } = new List<string>();
		[JsonPropertyName("jvm")]
		public List<string> JVM { get; set; } = new List<string>();
	}

}
