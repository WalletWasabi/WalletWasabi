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
		private string _warningMessage;
		private string _successMessage;
		public WalletViewModel Wallet { get; }

		public WalletService WalletService => Wallet.WalletService;
		public KeyManager KeyManager => WalletService.KeyManager;
		public bool IsWatchOnly => KeyManager.IsWatchOnly;
		public bool IsHardwareWallet => KeyManager.IsHardwareWallet;

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Wallet = walletViewModel;
			DoItCommand = ReactiveCommand.Create(DisplayActionTab);
		}

		public ReactiveCommand<Unit, Unit> DoItCommand { get; }

		public void DisplayActionTab()
		{
			IoC.Get<IShell>().AddOrSelectDocument(this);
			IoC.Get<IShell>().Select(this);
		}

		public void AddActionTab()
		{
			IoC.Get<IShell>().AddDocument(this,select: false);
		}

		protected void SetWarningMessage(string message)
		{
			SuccessMessage = "";
			WarningMessage = message;

			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await Task.Delay(7000);
				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}

		public void SetSuccessMessage(string message)
		{
			SuccessMessage = message;
			WarningMessage = "";

			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await Task.Delay(7000);
				if (SuccessMessage == message)
				{
					SuccessMessage = "";
				}
			});
		}

		public string WarningMessage
		{
			get => _warningMessage;
			set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
		}

		public string SuccessMessage
		{
			get => _successMessage;
			set => this.RaiseAndSetIfChanged(ref _successMessage, value);
		}
	}
}
