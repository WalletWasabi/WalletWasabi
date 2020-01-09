using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

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

			Console.WriteLine("Phase: finding the zip file on desktop which contains the compiled binaries from Windows.");

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
			var appNotarizeFilePath = Path.Combine(workingDir, $"Wasabi-{versionPrefix}.zip");
			var contentsPath = Path.GetFullPath(Path.Combine(Program.PackagerProjectDirectory.Replace("\\", "//"), "Content", "Osx"));
			var entitlementsPath = Path.Combine(contentsPath, "entitlements.plist");

			var signArguments = $"--sign \"L233B2JQ68\" --verbose --deep --force --options runtime --timestamp --entitlements \"{entitlementsPath}\"";

			Console.WriteLine("Phase: creating the working directory.");

			Console.WriteLine("Enter appleId (email):");
			var appleId = Console.ReadLine();
			Console.WriteLine("Enter password:");
			var password = Console.ReadLine();

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "chmod",
				Arguments = $"-R 775 \"{workingDir}\"",
			}))
			{
				process.WaitForExit();
			}

			IoHelpers.DeleteRecursivelyWithMagicDustAsync(workingDir).GetAwaiter().GetResult();
			Directory.CreateDirectory(workingDir);

			Console.WriteLine("Phase: creating the app.");

			IoHelpers.EnsureDirectoryExists(binPath);
			ZipFile.ExtractToDirectory(zipPath, binPath); // Copy the binaries.

			IoHelpers.CopyFilesRecursively(new DirectoryInfo(Path.Combine(contentsPath, "App")), new DirectoryInfo(appPath));

			Console.WriteLine("Update the plist file with current information for example with version.");

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

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "chmod",
				Arguments = $"-R u+rwX,go+rX,go-w \"{appPath}\"",
				WorkingDirectory = workingDir
			}))
			{
				process.WaitForExit();
			}

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "chmod",
				Arguments = $"-R u+x \"{Path.Combine(appPath, "Contents", "MacOS")}\"",
				WorkingDirectory = workingDir
			}))
			{
				process.WaitForExit();
			}

			Console.WriteLine("Signing the files in app.");

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

			Console.WriteLine("Phase: verifying the signature.");

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

			Console.WriteLine("Phase: notarize the app.");

			ZipFile.CreateFromDirectory(appPath, appNotarizeFilePath);
			Notarize(appleId, password, appNotarizeFilePath, bundleIdentifier);
			Staple(appPath);

			Console.WriteLine("Phase: creating the dmg.");

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

			Console.WriteLine("Phase: creating dmg.");

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "create-dmg",
				Arguments = string.Join(" ", dmgArguments),
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			Console.WriteLine("Phase: signing the dmg file.");

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"{signArguments} \"{dmgFilePath}\"",
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			Console.WriteLine("Phase: verifying the signature.");

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

			Console.WriteLine("Phase: notarize dmg");
			Notarize(appleId, password, dmgFilePath, bundleIdentifier);

			Console.WriteLine("Phase: staple dmp");
			Staple(dmgFilePath);

			Console.WriteLine("Phase: finish.");
		}

		public static bool IsMacSignMode(string[] args)
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX); // For now this is enough. If you run it on macOS you want to sign.
		}

		private static void Notarize(string appleId, string password, string filePath, string bundleIdentifier)
		{
			string uploadId = null;

			Console.WriteLine("Start notarizing, uploading file.");

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "xcrun",
				Arguments = $"altool --notarize-app -t osx -f \"{filePath}\" --primary-bundle-id \"{bundleIdentifier}\" -u \"{appleId}\" -p \"{password}\" --output-format xml",
				RedirectStandardOutput = true,
			}))
			{
				process.WaitForExit();
				string result = process.StandardOutput.ReadToEnd();

				if (result.Contains("The software asset has already been uploaded. The upload ID is"))
				{
					// Example: The software asset has already been uploaded. The upload ID is 7689dc08-d6c8-4783-8d28-33e575f5c967
					uploadId = result.Split('"').First(line => line.Contains("The software asset has already been uploaded.")).Split("The upload ID is").Last().Trim();
				}
				else if (result.Contains("No errors uploading"))
				{
					// Example: <key>RequestUUID</key>\n\t\t<string>2a2a164f-2ae7-4293-8357-5d5a5cdd580a</string>

					var lines = result.Split('\n');

					for (int i = 0; i < lines.Length; i++)
					{
						string line = lines[i].Trim();
						if (!line.TrimStart().StartsWith("<key>", StringComparison.InvariantCultureIgnoreCase))
						{
							continue;
						}

						if (line.Contains("<key>RequestUUID</key>", StringComparison.InvariantCulture))
						{
							uploadId = lines[i + 1].Trim().Replace("<string>", "").Replace("</string>", "");
						}
					}
				}
			}

			if (uploadId is null)
			{
				throw new InvalidOperationException("Cannot get uploadId. Notarization failed.");
			}

			while (true) // Wait for the notarization.
			{
				Console.WriteLine("Checking notarization status.");
				using var process = Process.Start(new ProcessStartInfo
				{
					FileName = "xcrun",
					Arguments = $"altool --notarization-info \"{uploadId}\" -u \"{appleId}\" -p \"{password}\"",
					RedirectStandardError = true,
				});
				process.WaitForExit();
				string result = process.StandardError.ReadToEnd();
				if (result.Contains("Status Message: Package Approved"))
				{
					break;
				}
				if (result.Contains("Status: in progress"))
				{
					Thread.Sleep(2000);
					continue;
				}
				if (result.Contains("Could not find the RequestUUID"))
				{
					Thread.Sleep(2000);
					continue;
				}

				throw new InvalidOperationException(result);
			}
		}

		private static void Staple(string filePath)
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "xcrun",
				Arguments = $"stapler staple \"{filePath}\"",
			});
			process.WaitForExit();
		}
	}
}
