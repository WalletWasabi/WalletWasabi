using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Helpers
{
	/// <summary>
	/// Helper methods for Application lifetime related functions.
	/// </summary>
	public static class AppLifetimeHelper
	{
		/// <summary>
		/// Attempts to restart the app without passing any program arguments.
		/// </summary>
		/// <remarks>
		/// This method is only functional on the published builds
		/// and not on debugging runs.
		/// </remarks>
		public static void Restart()
		{
			StartAppWithArgs();
			Shutdown();
		}

		/// <summary>
		/// Attempts to start a new instance of the app with optional program arguments
		/// </summary>
		/// <remarks>
		/// This method is only functional on the published builds
		/// and not on debugging runs.
		/// </remarks>
		/// <param name="args">The program arguments to pass to the new instance.</param>
		public static void StartAppWithArgs(string args = "")
		{
			var path = Process.GetCurrentProcess().MainModule?.FileName;
			var workingDir = new DirectoryInfo(path).Parent.ToString();

			if (string.IsNullOrEmpty(path))
			{
				throw new InvalidOperationException($"Invalid path: '{path}'");
			}

			var startInfo = new ProcessStartInfo
			{
				FileName = path,
				WorkingDirectory = workingDir,
				Arguments = args,
				UseShellExecute = false,
			};

			using var p = Process.Start(startInfo);
		}

		/// <summary>
		/// Attempts to shutdown the current instance of the app safely.
		/// </summary>
		public static void Shutdown()
		{
			(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}
	}
}