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
			if (files is null)
			{
				return;
			}

			foreach (var depsFilePath in files)
			{
				var lines = File.ReadAllLines(depsFilePath);

				List<string> outLines = new List<string>();
				foreach (var line in lines)
				{
					//      "sha512": "sha512-B0BYh5Fpeqp4GIbL5wEhde6M/dZ+s0tlXM0qMTvj4mTg9Rr4svVHGpn6dDp8pT2sB88ghxyLIpKGdx9Oj7f/pw==",
					if (line.Contains("\"sha512\": \"sha512-"))
					{
						//      "sha512": "",
						var lineToAdd = "      \"sha512\": \"\"";
						if (line.EndsWith(','))
						{
							lineToAdd += ',';
						}

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

		//public static void RemoveSosDocsUnix(string pathToSearch)
		//{
		//	// ToDo ?: Must remove from the ui.deps.json, too otherwise binary will fail.
		//	// Leave it to not do anything for now.
		//	string[] files = Directory.GetFiles(pathToSearch, "sosdocsunix.txt", SearchOption.AllDirectories);
		//	if (files is null) return;

		//	foreach (var sosDocsUnixFilePath in files)
		//	{
		//		File.Delete(sosDocsUnixFilePath);
		//	}
		//}

		public static string LinuxPathCombine(params string[] paths)
		{
			return LinuxPath(Path.Combine(paths));
		}

		public static string LinuxPath(string path)
		{
			return path.Replace(@"\", @"/");
		}

		public static long DirSize(DirectoryInfo d)
		{
			long size = 0;
			// Add file sizes.
			FileInfo[] fis = d.GetFiles();
			foreach (FileInfo fi in fis)
			{
				size += fi.Length;
			}
			// Add subdirectory sizes.
			DirectoryInfo[] dis = d.GetDirectories();
			foreach (DirectoryInfo di in dis)
			{
				size += DirSize(di);
			}
			return size;
		}
	}
}
