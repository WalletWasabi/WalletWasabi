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
		private object _dialogResult;
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
		}

		public void Select()
		{
			IoC.Get<IShell>().Select(this);
		}

		public void Close()
		{
			OnClose();
		}

		public async Task<object> ShowDialogAsync()
		{
			DialogResult = null;
			DisplayActionTab();

			while (!IsClosed)
			{
				if (!IsSelected) // Prevent de-selection of tab.
					Select();
				await Task.Delay(100);
			}
			return DialogResult;
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

		public object DialogResult
		{
			get => _dialogResult;
			set => this.RaiseAndSetIfChanged(ref _dialogResult, value);
		}
	}
}
