using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Backend
{
	public static class UnversionedWebBuilder
	{
		public static string UnversionedFolder { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\wwwroot\\unversioned"));

		public static string CreateFilePath(string fileName) => Path.Combine(UnversionedFolder, fileName);

		public static void CreateClientVersionHtml()
		{
			var filePath = CreateFilePath("client-version.html");
			var content = "<link href=\"txtstyle.css\" rel=\"stylesheet\" type=\"text/css\" />" + Environment.NewLine;
			content += Helpers.Constants.ClientVersion.ToString();

			File.WriteAllText(filePath, content);
		}
	}
}
