using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices
{
	public static class MicroserviceHelpers
	{
		public static string GetBinaryPath(string binaryNameWithoutExtension)
		{
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			string commonPartialPath = Path.Combine(fullBaseDirectory, "Microservices", "Binaries");
			string path;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				path = Path.Combine(commonPartialPath, $"{binaryNameWithoutExtension}-win64", $"{binaryNameWithoutExtension}.exe");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				path = Path.Combine(commonPartialPath, $"{binaryNameWithoutExtension}-lin64", binaryNameWithoutExtension);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				path = Path.Combine(commonPartialPath, $"{binaryNameWithoutExtension}-osx64", binaryNameWithoutExtension);
			}
			else
			{
				throw new NotSupportedException("Operating system is not supported.");
			}

			return path;
		}
	}
}
