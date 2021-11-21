using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class LibraryItemRule
	{
		[JsonPropertyName("action")]
		public string Action { get; set; }
		[JsonPropertyName("os")]
		public OperatingSystemInfo OS { get; set; }
		[JsonExtensionData]
		public Dictionary<string, object> NonMapped { get; set; }

		public class OperatingSystemInfo
		{
			[JsonPropertyName("name")]
			public string Name { get; set; }
		}
	}

}
