using System;

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
			catch (Exception)
			{
				// do nothing
			}

			return false;
		}
	}
}
