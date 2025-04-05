using System.Reactive.Disposables;
using System.Reactive.Linq;
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
	[AutoNotify] private byte? _shares;
	[AutoNotify] private byte? _threshold;

	private MultiShareOptionsViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);

		_shares = multiShareBackup.Settings.Shares;
		_threshold = multiShareBackup.Settings.Threshold;

		EnableBack = true;

		var nextCommandCanExecute = this.WhenAnyValue(
				x => x.Threshold,
				x => x.Shares,
				delegate
				{
					// This will fire validations before return canExecute value.
					this.RaisePropertyChanged(nameof(Threshold));
					this.RaisePropertyChanged(nameof(Shares));

					return OptionsAreValid() && !Validations.Any;
				})
			.ObserveOn(RxApp.MainThreadScheduler);

		NextCommand = ReactiveCommand.Create(() => OnNext(options), nextCommandCanExecute);

		CancelCommand = ReactiveCommand.Create(OnCancel);

		this.ValidateProperty(x => x.Shares, ValidateShares);
		this.ValidateProperty(x => x.Threshold, ValidateThreshold);
	}

	private bool OptionsAreValid()
	{
		if (_threshold is null || _shares is null)
		{
			return false;
		}

		return !(_threshold > WalletGenerator.MaxShamirThreshold)
		       && !(_threshold < WalletGenerator.MinShamirThreshold)
		       && !(_shares < WalletGenerator.MinShamirShares)
		       && !(_shares > WalletGenerator.MaxShamirShares)
		       && (_threshold != 1 || !(_shares > 1));
	}

	private void ValidateShares(IValidationErrors errors)
	{
		if (Shares is null)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"The {nameof(Shares)} cannot be empty");
		}

		if (Shares is < WalletGenerator.MinShamirShares or > WalletGenerator.MaxShamirShares)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {WalletGenerator.MinShamirShares} and {WalletGenerator.MaxShamirShares}.");
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
		if (Threshold is null)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"The {nameof(Threshold)} cannot be empty");
		}

		if (Threshold is < WalletGenerator.MinShamirThreshold or > WalletGenerator.MaxShamirThreshold)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Must be a number between {WalletGenerator.MinShamirThreshold} and {WalletGenerator.MaxShamirThreshold}.");
		}

		if (Threshold > Shares)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"{nameof(Threshold)} value can not be bigger then {nameof(Shares)} value.");
		}

		if (Threshold == 1 && Shares > 1)
		{
			errors.Add(
				ErrorSeverity.Error,
				$"Can only generate one share for {nameof(Threshold)} equal 1.");
		}
	}

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		if (options.SelectedWalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		if (!OptionsAreValid())
		{
			return;
		}

		if (_threshold is null || _shares is null)
		{
			return;
		}

		var shares = Shamir.Generate(
			_threshold.Value,
			_shares.Value,
			WalletGenerator.GenerateShamirEntropy());

		options = options with
		{
			SelectedWalletBackup = multiShareBackup with
			{
				Shares = shares,
				Settings = new MultiShareBackupSettings(_threshold.Value, _shares.Value),
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
