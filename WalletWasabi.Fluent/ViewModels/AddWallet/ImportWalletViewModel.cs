using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Newtonsoft.Json.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ImportWalletViewModel
	{
		public ImportWalletViewModel(string walletName, WalletManager walletManager)
		{
			Task.Run(ImportWallet)
				.ContinueWith(o =>
				{
					if (o.Exception is { } ex)
					{
						Logger.LogError(ex);
					}
				});
		}

		private async void ImportWallet()
		{
			var filePath = await GetFilePath();

			if (filePath is null)
			{
				return;
			}

			var json = JObject.Parse(await File.ReadAllTextAsync(filePath));

			/*
			 * Note for me.
			 * First check for possible wasabi wallet if not success, then
			 * check for Coldcard (Xpub, fingerprint exists?)
			 * order is important!
			 * still not success, throw
			 */
		}

		private async Task<string?> GetFilePath()
		{
			var ofd = new OpenFileDialog
			{
				AllowMultiple = false,
				Title = "Import wallet file",
				Directory = SetDefaultDirectory(),
			};

			var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime).MainWindow;
			var selected = await ofd.ShowAsync(window);

			if (selected is { } && selected.Any())
			{
				return selected.First();
			}

			return null;
		}

		private string SetDefaultDirectory()
		{
			// TODO: Test if this still needed
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Path.Combine("/media", Environment.UserName);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}

			// TODO: Windows default?
			return "";
		}
	}
}