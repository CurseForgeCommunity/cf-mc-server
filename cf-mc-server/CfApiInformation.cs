using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace CurseForge.Minecraft.Serverpack.Launcher
{
	partial class Program
	{
		private static void GetCfApiInformation(out string cfApiKey, out long cfPartnerId, out string cfContactEmail, out List<string> errors)
		{
			errors = new();
#if DEBUG
			cfApiKey = Environment.GetEnvironmentVariable("CFAPI_Key");
			_ = long.TryParse(Environment.GetEnvironmentVariable("CFAPI_PartnerId"), out cfPartnerId);
			cfContactEmail = Environment.GetEnvironmentVariable("CFAPI_ContactEmail");
#else
			cfApiKey = CFApiKey;
			cfPartnerId = -1;
			cfContactEmail = "serverlauncher@nolifeking85.tv";
#endif

			if (string.IsNullOrWhiteSpace(cfApiKey))
			{
				errors.Add("Error: Missing CurseForge API key in environment variable CFAPI_Key");
			}

			if (cfPartnerId == 0)
			{
				errors.Add("Error: Missing CurseForge Partner Id in environment variable CFAPI_PartnerId");
			}

			if (string.IsNullOrWhiteSpace(cfContactEmail) || !MailAddress.TryCreate(cfContactEmail, out _))
			{
				errors.Add("Error: Missing contact email for the API key in environment variable CFAPI_ContactEmail");
			}

			if (errors.Count > 0)
			{
				errors.ForEach(Console.WriteLine);
			}
		}
	}
}
