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
			if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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

			var unsignedDmgPath = files[0];
			var versionPrefix = unsignedDmgPath.Split('-').Last().TrimEnd(".zip", StringComparison.InvariantCultureIgnoreCase); // Example: "/Users/user/Desktop/Wasabi-unsigned-1.1.10.2.dmg".

			var dmgPath = Path.Combine(workingDir, "dmg");
			var appPath = Path.Combine(dmgPath, "Wasabi Wallet.app");
			var binPath = Path.Combine(appPath, "Contents","MacOS");

			// Copy the published files.
			IoHelpers.EnsureDirectoryExists(binPath);
			ZipFile.ExtractToDirectory(unsignedDmgPath, binPath);


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
