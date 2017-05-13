using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace HiddenWallet.Packager
{
    class Program
    {
        static void Main(string[] args)
		{
			var packagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
			var apiProjectDirectory = Path.Combine(packagerProjectDirectory, "..\\HiddenWallet.API");

			// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
			string[] targets =
			{
				"win7-x64",
				//"win8-x64",
				//"win81-x64",
				//"win10-x64"
			};
			UpdateCsproj(apiProjectDirectory, targets);

			var psiBuild = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = "build",
				WorkingDirectory = apiProjectDirectory
			};
			var pBuild = Process.Start(psiBuild);
			pBuild.WaitForExit();

			foreach (var target in targets)
			{
				var currDistDir = Path.Combine(apiProjectDirectory, "bin\\dist", target);
				if (Directory.Exists(currDistDir))
				{
					Directory.Delete(currDistDir, true);
				}
				Directory.CreateDirectory(currDistDir);

				var torFolderPath = Path.Combine(currDistDir, "tor");
				Console.WriteLine("Replacing tor...");
				if (Directory.Exists(torFolderPath))
				{
					Directory.Delete(torFolderPath, true);
				}
				ZipFile.ExtractToDirectory(Path.Combine(packagerProjectDirectory, "tor.zip"), currDistDir);

				var psiPublish = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = $"publish -r {target} --output bin/dist/{target}",
					WorkingDirectory = apiProjectDirectory
				};
				var pPublish = Process.Start(psiPublish);
				pPublish.WaitForExit();
			}

			Console.WriteLine("Finished. Press key to exit...");
			Console.ReadKey();
		}

		private static void UpdateCsproj(string apiProjectDirectory, string[] targets)
		{
			string csprojFile = Path.Combine(apiProjectDirectory, "HiddenWallet.API.csproj");
			var csprojString = File.ReadAllText(csprojFile);
			var csprojXml = new XmlDocument();
			csprojXml.LoadXml(csprojString);
			var csprojTargets = csprojXml.GetElementsByTagName("RuntimeIdentifiers")[0].InnerText.Split(';').ToList();
			var added = false;
			foreach (var target in targets)
			{
				if (!csprojTargets.Contains(target))
				{
					csprojTargets.Add(target);
					added = true;
				}
			}
			if (added)
			{
				csprojXml.GetElementsByTagName("RuntimeIdentifiers")[0].InnerText = string.Join(";", csprojTargets);
				using (var fs = new FileStream(csprojFile, FileMode.Create))
				{
					csprojXml.Save(fs);
				}
			}
		}
	}
}