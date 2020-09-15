using Avalonia.Threading;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Helpers
{
	public static class Utils
	{
		public static bool DetectLLVMPipeRasterizer()
		{
			try
			{
				var shellResult = ShellUtils.ExecuteShellCommand("glxinfo | grep renderer", "");

				if (!string.IsNullOrWhiteSpace(shellResult.Output) && shellResult.Output.Contains("llvmpipe"))
				{
					return true;
				}
			}
			catch
			{
				// do nothing
			}

			return false;
		}

		[FlagsAttribute]
		private enum EXECUTION_STATE : uint
		{
			ES_AWAYMODE_REQUIRED = 0x00000040,
			ES_CONTINUOUS = 0x80000000,
			ES_DISPLAY_REQUIRED = 0x00000002,
			ES_SYSTEM_REQUIRED = 0x00000001
			// Legacy flag, should not be used.
			// ES_USER_PRESENT = 0x00000004
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

		public static void KeepSystemAwake()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				SetThreadExecutionState(EXECUTION_STATE.ES_SYSTEM_REQUIRED);
			}
		}
	}
}
