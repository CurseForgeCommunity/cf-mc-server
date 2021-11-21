using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class CurseForgeFile
	{
		[JsonPropertyName("projectID")]
		public long ProjectId { get; set; }
		[JsonPropertyName("fileID")]
		public long FileId { get; set; }
		[JsonPropertyName("required")]
		public bool Required { get; set; }
	}

}
