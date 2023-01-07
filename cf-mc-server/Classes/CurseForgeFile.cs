using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class CurseForgeFile
	{
		[JsonPropertyName("projectID")]
		public uint ProjectId { get; set; }
		[JsonPropertyName("fileID")]
		public uint FileId { get; set; }
		[JsonPropertyName("required")]
		public bool Required { get; set; }
	}

}
