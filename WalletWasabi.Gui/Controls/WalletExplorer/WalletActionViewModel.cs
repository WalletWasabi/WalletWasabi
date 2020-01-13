using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletActionViewModel : WasabiDocumentTabViewModel
	{
		public WalletViewModel Wallet { get; }
		public WalletTab WalletTab { get; set; }

		public WalletService WalletService => Wallet.WalletService;
		public KeyManager KeyManager => WalletService.KeyManager;
		public bool IsWatchOnly => KeyManager.IsWatchOnly;
		public bool IsHardwareWallet => KeyManager.IsHardwareWallet;

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Wallet = walletViewModel;
		}

		public WalletActionViewModel(string title, WalletViewModel walletViewModel, WalletTab walletTab)
			: this(title, walletViewModel)
		{
			WalletTab = walletTab;
		}
	}
}
