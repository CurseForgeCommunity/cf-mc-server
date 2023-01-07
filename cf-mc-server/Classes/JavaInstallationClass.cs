using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class JavaInstallationClass
	{
		[JsonPropertyName("files")]
		public Dictionary<string, JavaInstallationFile> Files { get; set; } = new Dictionary<string, JavaInstallationFile>();

		public class JavaInstallationFile
		{
			[JsonPropertyName("downloads")]
			public Dictionary<string, ArtifactInfo> Downloads { get; set; }
			[JsonPropertyName("executable")]
			public bool Executable { get; set; }
			[JsonPropertyName("type")]
			public string Type { get; set; }
		}
	}

}
