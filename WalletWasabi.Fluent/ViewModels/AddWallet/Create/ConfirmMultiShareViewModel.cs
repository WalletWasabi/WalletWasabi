using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Multi-share")]
public partial class ConfirmMultiShareViewModel : RoutableViewModel
{
	private readonly WalletCreationOptions.AddNewWallet _options;

	[AutoNotify] private byte _currentShare;
	[AutoNotify] private byte _totalShares;

	private ConfirmMultiShareViewModel(WalletCreationOptions.AddNewWallet options)
	{
		_options = options;

		var multiShareBackup = options.WalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		// TODO:
		// ArgumentNullException.ThrowIfNull(multiShareBackup.Share);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Settings);

		_currentShare = multiShareBackup.CurrentShare;
		_totalShares = multiShareBackup.Settings.Shares;

	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		// TODO:

		EnableBack = true;

		CancelCommand = ReactiveCommand.Create(OnCancel);

		// TODO:
		var nextCommandCanExecute =
			Observable.Return(true);

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		// TODO:

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}

	private async Task OnNextAsync()
	{
		var options = _options;

		if (options.WalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		if (_currentShare < multiShareBackup.Settings.Shares)
		{
			options = options with
			{
				WalletBackup = multiShareBackup with
				{
					CurrentShare = ++_currentShare
				}
			};

			// TODO:
			Navigate().To().ConfirmMultiShare(options);
		}
		else
		{
			var dialogCaption = "Store your passphrase safely, it cannot be reset if lost.\n" +
			                    "It's needed to open and to recover your wallet.\n" +
			                    "It's a recovery words extension for more security.";
			var password = await Navigate().To().CreatePasswordDialog("Add Passphrase", dialogCaption, enableEmpty: true).GetResultAsync();

			if (password is { })
			{
				options = options with
				{
					WalletBackup = multiShareBackup with
					{
						Password = password
					}
				};
			}

			// TODO: Implement new wallet creation with Shares.
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);
			Navigate().To().AddedWalletPage(walletSettings, options!);
		}
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}
}
