using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WalletWasabi.Packager
{
	public static class Tools
	{
		public static void ClearSha512Tags(string pathToSearch)
		{
			var files = Directory.GetFiles(pathToSearch, "*.deps.json"); //https://natemcmaster.com/blog/2017/12/21/netcore-primitives/
			if (files == null || files.Length == 0) return;

			var depsFilePath = files[0];

			var lines = File.ReadAllLines(depsFilePath);

			List<string> outLines = new List<string>();
			foreach (var line in lines)
			{
				//      "sha512": "sha512-B0BYh5Fpeqp4GIbL5wEhde6M/dZ+s0tlXM0qMTvj4mTg9Rr4svVHGpn6dDp8pT2sB88ghxyLIpKGdx9Oj7f/pw==",
				if (line.Contains("\"sha512\": \"sha512-"))
				{
					//      "sha512": "",
					var lineToAdd = "      \"sha512\": \"\"";
					if (line.EndsWith(',')) lineToAdd += ',';
					outLines.Add(lineToAdd);
				}
				else
				{
					outLines.Add(line);
				}
			}
			File.Delete(depsFilePath);
			File.WriteAllLines(depsFilePath, outLines);
		}
	}
}
