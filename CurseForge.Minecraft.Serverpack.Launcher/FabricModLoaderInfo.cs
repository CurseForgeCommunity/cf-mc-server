using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class FabricModLoaderInfo : ModLoaderVersionInfo
	{
		[JsonPropertyName("libraries")]
		public List<FabricLibraryInfo> Libraries { get; set; }
	}

}
