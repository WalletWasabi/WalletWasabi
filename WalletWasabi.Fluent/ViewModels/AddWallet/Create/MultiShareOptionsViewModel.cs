using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Multi-share Options")]
public partial class MultiShareOptionsViewModel : RoutableViewModel
{
	[AutoNotify] private byte _shares;
	[AutoNotify] private byte _threshold;

	private MultiShareOptionsViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var multiShareBackup = options.WalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		// TODO:
		// ArgumentNullException.ThrowIfNull(multiShareBackup.Share);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Settings);

		_shares = multiShareBackup.Settings.Shares;
		_threshold = multiShareBackup.Settings.Threshold;

		EnableBack = true;

		// TODO: Add validation
		NextCommand = ReactiveCommand.Create(() => OnNext(options));

		// TODO: Add validation

		CancelCommand = ReactiveCommand.Create(OnCancel);
	}

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		if (options.WalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		// TODO: Validate shares and threshold

		options = options with
		{
			WalletBackup = multiShareBackup with
			{
				Settings = new MultiShareBackupSettings(_threshold, _shares),
				CurrentShare = 1
			}
		};

		Navigate().To().ConfirmMultiShare(options);
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}
}
