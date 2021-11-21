using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class ForgeModLoaderInfo : ModLoaderVersionInfo
	{
		[JsonPropertyName("jar")]
		public string Jar { get; set; }

		[JsonPropertyName("libraries")]
		public List<ForgeLibraryInfo> Libraries { get; set; }
	}

}
