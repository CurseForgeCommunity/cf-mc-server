using System;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	public class LibraryDownloadItem
	{
		public LibraryDownloadItem(string packageName, string filePath, Uri url)
		{
			PackageName = packageName;
			FilePath = filePath;
			Url = url;
		}

		public string PackageName { get; set; }
		public string FilePath { get; set; }
		public Uri Url { get; set; }
	}

}
