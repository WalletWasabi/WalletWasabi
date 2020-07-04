using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WalletWasabi.Microservices
{
	public class ProcessBuilder
	{
		public static Process BuildProcessInstance(string processPath, string arguments, bool openConsole = false)
		{
			ProcessWindowStyle windowStyle;
			if (openConsole)
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					throw new PlatformNotSupportedException($"{RuntimeInformation.OSDescription} is not supported.");
				}

				windowStyle = ProcessWindowStyle.Normal;
			}
			else
			{
				windowStyle = ProcessWindowStyle.Hidden;
			}

			var p = new Process();
			p.StartInfo.FileName = processPath;
			p.StartInfo.Arguments = arguments;
			p.StartInfo.RedirectStandardOutput = !openConsole;
			p.StartInfo.UseShellExecute = openConsole;
			p.StartInfo.CreateNoWindow = !openConsole;
			p.StartInfo.WindowStyle = windowStyle;

			return p;
		}
	}
}
