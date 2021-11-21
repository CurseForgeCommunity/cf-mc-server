using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class FabricLibraryInfo : LibraryItemInfo
	{
		public MavenString MavenInfo
		{
			get { return new MavenString(Name); }
		}
		[JsonPropertyName("url")]
		public string Url { get; set; }
	}

}
