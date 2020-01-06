using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.Packager
{
	public static class MacSignTools
	{
		public static void Sign()
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				throw new NotSupportedException("This signing methon only valid on macOS!");
			}

			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

			var srcZipFileNamePattern = "Wasabi-osx-*.zip";

			var files = Directory.GetFiles(desktopPath, srcZipFileNamePattern); //Example: Wasabi-osx-1.1.10.2.zip
			if (files.Length != 1)
			{
				throw new InvalidDataException($"{srcZipFileNamePattern} file missing or there are more on Desktop! There must be exactly one!");
			}

			var workingDir = Path.Combine(desktopPath, "wasabiTemp");
			IoHelpers.DeleteRecursivelyWithMagicDustAsync(workingDir).GetAwaiter().GetResult();
			Directory.CreateDirectory(workingDir);

			var zipPath = files[0];
			var versionPrefix = zipPath.Split('-').Last().TrimEnd(".zip", StringComparison.InvariantCultureIgnoreCase); // Example: "/Users/user/Desktop/Wasabi-unsigned-1.1.10.2.dmg".

			var dmgPath = Path.Combine(workingDir, "dmg");
			var appName = "Wasabi Wallet.app";
			var appPath = Path.Combine(dmgPath, appName);
			var binPath = Path.Combine(appPath, "Contents", "MacOS");

			// Copy the published files.
			IoHelpers.EnsureDirectoryExists(binPath);
			ZipFile.ExtractToDirectory(zipPath, binPath);

			var contentsPath = Path.GetFullPath(Path.Combine(Program.PackagerProjectDirectory.Replace("\\", "//"), "Content", "Osx"));
			IoHelpers.CopyFilesRecursively(new DirectoryInfo(Path.Combine(contentsPath, "App")), new DirectoryInfo(appPath));
			var infoFilePath = Path.Combine(appPath, "Contents", "Info.plist");
			var lines = File.ReadAllLines(infoFilePath);
			string bundleIdentifier = null;

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if (!line.TrimStart().StartsWith("<key>", StringComparison.InvariantCultureIgnoreCase))
				{
					continue;
				}

				if (line.Contains("CFBundleShortVersionString", StringComparison.InvariantCulture) ||
					line.Contains("CFBundleVersion", StringComparison.InvariantCulture))
				{
					lines[i + 1] = $"<string>{versionPrefix}</string>";
				}
				else if (line.Contains("CFBundleIdentifier", StringComparison.InvariantCulture))
				{
					bundleIdentifier = lines[i + 1];
				}
			}

			IoHelpers.DeleteRecursivelyWithMagicDustAsync(infoFilePath).GetAwaiter().GetResult();
			File.WriteAllLines(infoFilePath,lines);

			var entitlementsPath = Path.Combine(contentsPath, "entitlements.plist");
			if (!File.Exists(entitlementsPath))
			{
				throw new FileNotFoundException($"entitlements.plist file missing: {entitlementsPath}");
			}

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"--sign \"L233B2JQ68\" --verbose --deep --force --options runtime --timestamp --entitlements \"{entitlementsPath}\" \"{appName}\"",
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

		}

		public static bool IsMacSignMode(string[] args)
		{
			return true; // TODO: debug purposes, remove this before final merge.
			bool macSign = false;
			if (args != null)
			{
				foreach (var arg in args)
				{
					var targ = arg.Trim().TrimStart('-');
					if (targ.Equals("reduceonions", StringComparison.OrdinalIgnoreCase) ||
						targ.Equals("reduceonion", StringComparison.OrdinalIgnoreCase))
					{
						macSign = true;
						break;
					}
				}
			}

			return macSign;
		}
	}
}
