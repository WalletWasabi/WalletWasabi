using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public enum WalletJsonType
	{
		Unknown = 0,
		Wasabi = 1,
		Coldcard = 2,
	}

	public class ImportWalletViewModel : RoutableViewModel
	{
		public ImportWalletViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, string walletName, WalletManager walletManager)
			: base(navigationState, navigationTarget)
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

			switch (GetWalletType(filePath))
			{
				case WalletJsonType.Coldcard:
					break;
				case WalletJsonType.Wasabi:
					break;
				default:
					throw new FileLoadException("Unknown wallet file.");
			}

			ClearNavigation();
		}

		private WalletJsonType GetWalletType(string filePath)
		{

		}

		private async Task<string?> GetFilePath()
		{
			var ofd = new OpenFileDialog
			{
				AllowMultiple = false,
				Title = "Import Coldcard",
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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Path.Combine("/media", Environment.UserName);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			}

			return "";
		}
	}
}