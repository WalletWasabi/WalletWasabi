using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WalletWasabi.Gui.CommandLine
{
	static class Native
	{
		[DllImport("kernel32", SetLastError = true)]
		private static extern bool AttachConsole(int dwProcessId);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

		[DllImport("kernel32", SetLastError = true)]
		static extern bool FreeConsole();

		static bool _isConsoleAttached = false;

		public static void AttachParentConsole()
		{
			IntPtr ptr = GetForegroundWindow();

			GetWindowThreadProcessId(ptr, out int u);
			var process = Process.GetProcessById(u);

			if (AttachConsole(process.Id))
			{
				_isConsoleAttached = true;
			}
		}
		public static void DettachParentConsole()
		{
			if (_isConsoleAttached)
			{
				FreeConsole();
			}
		}
	}
}
