using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletActionViewModel : WasabiDocumentTabViewModel
	{
		public WalletViewModel Wallet { get; }

		public WalletService WalletService => Wallet.WalletService;
		public KeyManager KeyManager => WalletService.KeyManager;
		public bool IsWatchOnly => KeyManager.IsWatchOnly;
		public bool IsHardwareWallet => KeyManager.IsHardwareWallet;

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(walletViewModel.Global, title)
		{
			Wallet = walletViewModel;
		}
	}
}
