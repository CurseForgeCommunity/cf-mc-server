using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class ModLoader
	{
		[JsonPropertyName("id")]
		public string Id { get; set; }
		[JsonPropertyName("primary")]
		public bool Primary { get; set; }
	}

}
