using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class Download
	{
		[JsonPropertyName("artifact")]
		public ArtifactInfo Artifact { get; set; }

		[JsonPropertyName("classifiers")]
		public Dictionary<string, ArtifactInfo> Classifiers { get; set; } = new Dictionary<string, ArtifactInfo>();

		[JsonExtensionData]
		public Dictionary<string, object> NonMapped { get; set; }
	}

}
