using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Multi-share Options")]
public partial class MultiShareOptionsViewModel : RoutableViewModel
{
	[AutoNotify] private byte _shares;
	[AutoNotify] private byte _threshold;

	private MultiShareOptionsViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Shares);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Settings);

		_shares = multiShareBackup.Settings.Shares;
		_threshold = multiShareBackup.Settings.Threshold;

		EnableBack = true;

		var nextCommandCanExecute =
			this.WhenAnyValue(
					x => x.Threshold,
					x => x.Shares)
				.Select(_ => !Validations.Any);

		NextCommand = ReactiveCommand.Create(() => OnNext(options), nextCommandCanExecute);

		CancelCommand = ReactiveCommand.Create(OnCancel);

		this.ValidateProperty(x => x.Shares, ValidateShares);
		this.ValidateProperty(x => x.Threshold, ValidateThreshold);
	}

	private void ValidateShares(IValidationErrors errors)
	{
		if (Shares is < KeyManager.MinShamirShares or > KeyManager.MaxShamirShares)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {KeyManager.MinShamirShares} and {KeyManager.MaxShamirShares}.");
		}

		if (Shares < Threshold)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"{nameof(Shares)} value can not be lower then {nameof(Threshold)} value.");
		}
	}

	private void ValidateThreshold(IValidationErrors errors)
	{
		if (Threshold is < KeyManager.MinShamirThreshold or > KeyManager.MaxShamirThreshold)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {KeyManager.MinShamirThreshold} and {KeyManager.MaxShamirThreshold}.");
		}

		if (Threshold > Shares)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"{nameof(Threshold)} value can not be bigger then {nameof(Shares)} value.");
		}
	}

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		if (options.SelectedWalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		// TODO: Validate shares and threshold
#if false
		var shares = Shamir.Generate(
			_threshold,
			_shares,
			KeyManager.GenerateShamirEntropy());
#else
		// TODO: Debug code
		var shares = Shamir.Generate(
			groupThreshold: 2,
			groups:
			[
				// Alice group shares. 1 is enough to reconstruct a group share,
				// therefore she needs at least two group shares to be reconstructed,
				(1, 1),
				(1, 1),
				// 3 of 5 Friends' shares are required to reconstruct this group share
				(3, 5),
				// 2 of 6 Family's shares are required to reconstruct this group share
				(2, 6),
			],
			seed: "ABCDEFGHIJKLMNOP"u8.ToArray(),
			passphrase: "TREZOR");
#endif

		options = options with
		{
			SelectedWalletBackup = multiShareBackup with
			{
				Shares = shares,
				Settings = new MultiShareBackupSettings(_threshold, _shares),
				CurrentShare = 1
			}
		};

		Navigate().To().MultiShare(options);
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
