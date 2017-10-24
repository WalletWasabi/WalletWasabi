using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HiddenWallet.FullSpvWallet
{
	public static class Global
	{
		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (_dataDir != null) return _dataDir;

				var appName = "HiddenWallet";

				string directory = null;

				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var home = Environment.GetEnvironmentVariable("HOME");
					if (!string.IsNullOrEmpty(home))
					{
						Console.WriteLine("Using HOME environment variable for initializing application data.");
						directory = Path.Combine(home, "." + appName.ToLowerInvariant());
					}
					else
					{
						throw new DirectoryNotFoundException("Could not find suitable datadir");
					}
				}
				else
				{
					var localAppData = Environment.GetEnvironmentVariable("APPDATA");
					if (!string.IsNullOrEmpty(localAppData))
					{
						Console.WriteLine("Using APPDATA environment variable for initializing application data.");
						directory = Path.Combine(localAppData, appName);
					}
					else
					{
						throw new DirectoryNotFoundException("Could not find suitable datadir");
					}
				}

				if (!Directory.Exists(directory))
				{
					Debug.WriteLine("Creating data directory...");
					Directory.CreateDirectory(directory);
				}

				_dataDir = directory;
				return _dataDir;
			}
		}
	}
}
