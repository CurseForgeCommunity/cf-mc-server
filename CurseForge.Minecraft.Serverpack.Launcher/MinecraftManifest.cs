using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MinecraftManifest : CurseForgeManifest
	{
		[JsonPropertyName("minecraft")]
		public MinecraftInfo Minecraft { get; set; }
	}

}
