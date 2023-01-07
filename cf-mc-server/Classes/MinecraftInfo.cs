using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MinecraftInfo
	{
		[JsonPropertyName("version")]
		public string Version { get; set; }
		[JsonPropertyName("modLoaders")]
		public List<ModLoader> ModLoaders { get; set; } = new List<ModLoader>();
	}

}
