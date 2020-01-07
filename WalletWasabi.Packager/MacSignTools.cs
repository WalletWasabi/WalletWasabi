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

			// Phase: finding the zip file on desktop which contains the compiled binaries from Windows.
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

			var srcZipFileNamePattern = "Wasabi-osx-*.zip";
			var files = Directory.GetFiles(desktopPath, srcZipFileNamePattern); //Example: Wasabi-osx-1.1.10.2.zip
			if (files.Length != 1)
			{
				throw new InvalidDataException($"{srcZipFileNamePattern} file missing or there are more on Desktop! There must be exactly one!");
			}
			var zipPath = files[0];
			var versionPrefix = zipPath.Split('-').Last().TrimEnd(".zip", StringComparison.InvariantCultureIgnoreCase); // Example: "/Users/user/Desktop/Wasabi-unsigned-1.1.10.2.dmg".

			var workingDir = Path.Combine(desktopPath, "wasabiTemp");
			var dmgPath = Path.Combine(workingDir, "dmg");
			var appName = "Wasabi Wallet.app";
			var appPath = Path.Combine(dmgPath, appName);
			var binPath = Path.Combine(appPath, "Contents", "MacOS");
			var resPath = Path.Combine(appPath, "Contents", "Resources");
			var infoFilePath = Path.Combine(appPath, "Contents", "Info.plist");
			var dmgFilePath = Path.Combine(workingDir, $"Wasabi-{versionPrefix}.dmg");
			var contentsPath = Path.GetFullPath(Path.Combine(Program.PackagerProjectDirectory.Replace("\\", "//"), "Content", "Osx"));
			var entitlementsPath = Path.Combine(contentsPath, "entitlements.plist");

			var signArguments = $"--sign \"L233B2JQ68\" --verbose --deep --force --options runtime --timestamp --entitlements \"{entitlementsPath}\"";
			// Phase: creating the working directory.

			IoHelpers.DeleteRecursivelyWithMagicDustAsync(workingDir).GetAwaiter().GetResult();
			Directory.CreateDirectory(workingDir);


			// Phase: creating the app.

			IoHelpers.EnsureDirectoryExists(binPath);
			ZipFile.ExtractToDirectory(zipPath, binPath); // Copy the binaries.


			IoHelpers.CopyFilesRecursively(new DirectoryInfo(Path.Combine(contentsPath, "App")), new DirectoryInfo(appPath));

			// Update the plist file with current information for example with version.

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
					lines[i + 1] = lines[i + 1].Replace("?", $"{versionPrefix}");
				}
				else if (line.Contains("CFBundleIdentifier", StringComparison.InvariantCulture))
				{
					bundleIdentifier = lines[i + 1].Trim().Replace("<string>", "").Replace("</string>", "");
				}
			}
			IoHelpers.DeleteRecursivelyWithMagicDustAsync(infoFilePath).GetAwaiter().GetResult();
			File.WriteAllLines(infoFilePath, lines);

			// Signing the files in app.

			if (!File.Exists(entitlementsPath))
			{
				throw new FileNotFoundException($"entitlements.plist file missing: {entitlementsPath}");
			}

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"{signArguments} \"{appPath}\"",
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			// Phase: verify the sign.

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"-dv --verbose=4 \"{appPath}\"",
				WorkingDirectory = dmgPath,
				RedirectStandardError = true,
			}))
			{
				process.WaitForExit();
				string result = process.StandardError.ReadToEnd();
				if (!result.Contains("Authority=Developer ID Application: zkSNACKs Ltd."))
				{
					throw new InvalidOperationException(result);
				}
			}

			// Phase: creating the dmg.
			if (File.Exists(dmgFilePath))
			{
				File.Delete(dmgFilePath);
			}

			var dmgArguments = new string[]
			{
				"--volname \"Wallet Wasabi\"",
				$"--volicon \"{Path.Combine(resPath, "WasabiLogo.icns")}\"",
				$"--background \"{Path.Combine(contentsPath, "Logo_with_text_small.png")}\"",
				"--window-pos 200 120",
				"--window-size 600 450",
				"--icon-size 100",
				"--icon \"Wasabi Wallet.app\" 100 150",
				"--hide-extension \"Wasabi Wallet.app\"",
				"--app-drop-link 500 150",
				"--no-internet-enable",
				$"\"{dmgFilePath}\"",
				$"\"{appPath}\""
			};

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "create-dmg",
				Arguments = string.Join(" ", dmgArguments),
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			// Phase: signing the dmg file.

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"{signArguments} \"{dmgFilePath}\"",
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			// Phase: verify the sign.

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"-dv --verbose=4 \"{appPath}\"",
				WorkingDirectory = dmgPath,
				RedirectStandardError = true,
			}))
			{
				process.WaitForExit();
				string result = process.StandardError.ReadToEnd();
				if (!result.Contains("Authority=Developer ID Application: zkSNACKs Ltd."))
				{
					throw new InvalidOperationException(result);
				}
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
