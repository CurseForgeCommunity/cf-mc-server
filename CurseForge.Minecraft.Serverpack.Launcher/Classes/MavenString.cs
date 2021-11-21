using System;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class MavenString
	{
		private string _data;

		public MavenString(string data, string extension = "jar")
		{
			_data = data;

			PackageName = _data;
			FileExtension = extension;

			var packageData = _data.Split(':');
			if (packageData.Length > 1)
			{
				Package = packageData[0];
				Name = packageData[1];

				if (packageData.Length > 2)
				{
					Version = packageData[2];
				}
			}
		}

		public string PackageName { get; set; }

		public string Package { get; set; }
		public string Name { get; set; }

		public string FileExtension { get; set; }
		public string Version { get; set; }

		public static implicit operator string(MavenString maven) => maven._data;
		public static implicit operator MavenString(string data) => new MavenString(data);

		public override string ToString()
		{
			return _data;
		}

		public string GetDownloadUrl(string baseUrl)
		{
			UriBuilder ub = new UriBuilder(baseUrl);

			ub.Path = $"{Package.Replace(".", "/")}/{Name}/{Version}/{Name}-{Version}.{FileExtension}";

			return ub.ToString();
		}
	}

}
