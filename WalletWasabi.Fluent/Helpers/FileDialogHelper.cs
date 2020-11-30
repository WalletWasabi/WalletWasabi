using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Helpers
{
	public static class FileDialogHelper
	{
		public static async Task<string?> ShowOpenFileDialogAsync(string title)
		{
			var ofd = new OpenFileDialog
			{
				AllowMultiple = false,
				Title = title,
				Directory = GetDefaultDirectory(),
			};

			var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow;
			var selected = await ofd.ShowAsync(window);

			return selected.FirstOrDefault();
		}

		private static string GetDefaultDirectory()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Path.Combine("/media", Environment.UserName);
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}

			return Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
		}
	}
}