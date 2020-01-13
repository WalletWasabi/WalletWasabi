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
				throw new NotSupportedException("This signing method is only valid on macOS!");
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
			var versionPrefix = zipPath.Split('-').Last().TrimEnd(".zip", StringComparison.InvariantCultureIgnoreCase); // Example: "/Users/user/Desktop/Wasabi-unsigned-1.1.10.2.zip".
			var workingDir = Path.Combine(desktopPath, "wasabiTemp");
			var dmgPath = Path.Combine(workingDir, "dmg");
			var appName = "Wasabi Wallet.app";
			var appPath = Path.Combine(dmgPath, appName);
			var binPath = Path.Combine(appPath, "Contents", "MacOS");
			var resPath = Path.Combine(appPath, "Contents", "Resources");
			var infoFilePath = Path.Combine(appPath, "Contents", "Info.plist");
			var dmgFileName = $"Wasabi-{versionPrefix}.dmg";
			var dmgFilePath = Path.Combine(workingDir, dmgFileName);
			var appNotarizeFilePath = Path.Combine(workingDir, $"Wasabi-{versionPrefix}.zip");
			var contentsPath = Path.GetFullPath(Path.Combine(Program.PackagerProjectDirectory.Replace("\\", "//"), "Content", "Osx"));
			var entitlementsPath = Path.Combine(contentsPath, "entitlements.plist");
			var entitlementsSandBoxPath = Path.Combine(contentsPath, "entitlements_sandbox.plist");
			var torZipDirPath = Path.Combine(binPath, "TorDaemons", "tor-osx64");
			var torZipPath = $"{torZipDirPath}.zip";
			var desktopDmgFilePath = Path.Combine(desktopPath, dmgFileName);

			var signArguments = $"--sign \"L233B2JQ68\" --verbose --options runtime --timestamp";

			Console.WriteLine("Phase: creating the working directory.");

			Console.WriteLine("Enter appleId (email):");
			var appleId = Console.ReadLine();
			Console.WriteLine("Enter password:");
			var password = Console.ReadLine();

			if (Directory.Exists(workingDir))
			{
				DeleteWithChmod(workingDir);
			}

			if (File.Exists(desktopDmgFilePath))
			{
				File.Delete(desktopDmgFilePath);
			}

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
					lines[i + 1] = lines[i + 1].Replace("?", $"{Version.Parse(versionPrefix).ToString(3)}"); // Apple allow only 3 version tags in plist.
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

			var filesToCheck = new[] { entitlementsPath, entitlementsSandBoxPath, torZipPath };

			foreach (var file in filesToCheck)
			{
				if (!File.Exists(file))
				{
					throw new FileNotFoundException($"File missing: {file}");
				}
			}

			// Tor already signed by: The Tor Project, Inc (MADPSAYN6T)

			// Can be automated: find -H YourAppBundle -print0 | xargs -0 file | grep "Mach-O .*executable"
			var exacutableFileNames = new[] { "wassabee","bitcoind","hwi"};

			Console.WriteLine("Signing the files in app.");

			UnlockKeychain();

			// Wassabee has to be signed at the end. Otherwise codesing witt throw a submodule not signed error.
			foreach (var file in Directory.GetFiles(appPath, "*.*", SearchOption.AllDirectories).OrderBy(file => new FileInfo(file).Name == "wassabee").ToList())
			{
				
				var fileName = new FileInfo(file).Name;

				if (fileName == ".DS_Store")
				{
					File.Delete(file);
					continue;
				}

				var isExecutable = exacutableFileNames.Contains(fileName);

				if (isExecutable)
				{
					using var process = Process.Start(new ProcessStartInfo
					{
						FileName = "chmod",
						Arguments = $"u+x \"{file}\"",
						WorkingDirectory = workingDir

					});
					process.WaitForExit();
				}

				var entitlementArgs = isExecutable ? entitlementsSandBoxPath : entitlementsPath;

				Sign($"{signArguments} --entitlements \"{entitlementArgs}\" \"{file}\"", dmgPath);

			}

			Console.WriteLine("Phase: verifying the signature.");

			Verify(appPath);

			// Building a package - this is not required now. This code will be useful later - do not delete!
			//var pkgPath = Path.Combine(workingDir, $"Wasabi-{versionPrefix}.pkg");
			//using (var process = Process.Start(new ProcessStartInfo
			//{
			//	FileName = "productbuild",
			//	Arguments = $"--component \"{appPath}\" /Applications \"{pkgPath}\" --sign \"L233B2JQ68\"",
			//	WorkingDirectory = dmgPath
			//}))
			//{
			//	process.WaitForExit();
			//}

			Console.WriteLine("Phase: notarize the app.");

			ZipFile.CreateFromDirectory(appPath, appNotarizeFilePath);
			Notarize(appleId, password, appNotarizeFilePath, bundleIdentifier);
			Staple(appPath);

			Console.WriteLine("Phase: creating the dmg.");

			if (File.Exists(dmgFilePath))
			{
				File.Delete(dmgFilePath);
			}

			var dmgArguments = string.Join(" ", new string[]
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
			});

			Console.WriteLine("Phase: creating dmg.");

			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "create-dmg",
				Arguments = dmgArguments,
				WorkingDirectory = dmgPath
			}))
			{
				process.WaitForExit();
			}

			Console.WriteLine("Phase: signing the dmg file.");

			UnlockKeychain();

			Sign($"{signArguments} --entitlements \"{entitlementsPath}\" \"{dmgFilePath}\"", dmgPath);

			Console.WriteLine("Phase: verifying the signature.");

			Verify(dmgFilePath);

			Console.WriteLine("Phase: notarize dmg");
			Notarize(appleId, password, dmgFilePath, bundleIdentifier);

			Console.WriteLine("Phase: staple dmp");
			Staple(dmgFilePath);

			File.Move(dmgFilePath, desktopDmgFilePath);
			DeleteWithChmod(workingDir);
			File.Delete(zipPath);

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

			Stopwatch sw = new Stopwatch();
			sw.Start();
			while (true) // Wait for the notarization.
			{
				Console.WriteLine($"Checking notarization status. Elapsed time: {sw.Elapsed.ToString()}");
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
					Thread.Sleep(4000);
					continue;
				}
				if (result.Contains("Could not find the RequestUUID"))
				{
					Thread.Sleep(4000);
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

		private static void DeleteWithChmod(string path)
		{
			using (var process = Process.Start(new ProcessStartInfo
			{
				FileName = "chmod",
				Arguments = $"-R 775 \"{path}\"",
			}))
			{
				process.WaitForExit();
			}

			IoHelpers.DeleteRecursivelyWithMagicDustAsync(path).GetAwaiter().GetResult();
		}

		private static void UnlockKeychain()
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "security",
				Arguments = $"unlock-keychain -p \"mysecretpassword\" build.keychain",
			});
			process.WaitForExit();
		}

		private static void Sign(string arguments,string workingDir)
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = arguments,
				WorkingDirectory = workingDir,
				RedirectStandardError = true
			});
			process.WaitForExit();
			var result = process.StandardError.ReadToEnd();
			if (result.Contains("code object is not signed at all"))
			{
				throw new InvalidOperationException(result);
			}
			Console.WriteLine(result.Trim());
		}

		private static void Verify(string path)
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = "codesign",
				Arguments = $"-dv --verbose=4 \"{path}\"",
				RedirectStandardError = true,
			});
			process.WaitForExit();
			string result = process.StandardError.ReadToEnd();
			if (!result.Contains("Authority=Developer ID Application: zkSNACKs Ltd."))
			{
				throw new InvalidOperationException(result);
			}
		}
	}
}
