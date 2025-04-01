using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets.Slip39;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Multi-share Words")]
public partial class RecoverMultiShareWalletViewModel : RoutableViewModel
{
	private readonly Share[]? _shares;

	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Share? _share;
	[AutoNotify] private bool _isMnemonicsValid;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte? _currentShare;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte? _requiredShares;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string? _caption;

	private RecoverMultiShareWalletViewModel(WalletCreationOptions.RecoverWallet options)
	{
		var multiShareBackup = options.WalletBackup as MultiShareBackup;

		_currentShare = multiShareBackup?.CurrentShare;
		_requiredShares = multiShareBackup?.Settings.Threshold;
		_shares = multiShareBackup?.Shares;

		Caption = _currentShare is not null && _requiredShares is not null
			? $"Enter share #{_currentShare + 1} of required {_requiredShares} shares."
			: "Enter any share";

		Suggestions = WordList.Wordlist;

		Share? ToShare()
		{
			try
			{
				return Share.FromMnemonic(GetTagsAsConcatString(Mnemonics).ToLowerInvariant());
			}
			catch (Exception)
			{
				return null;
			}
		}

		Mnemonics.ToObservableChangeSet().ToCollection()
			// Share.MIN_MNEMONIC_LENGTH_WORDS is 20 so we do not accept 12 or 18 words.
			.Select(x => x.Count is 20 or 24 or 33
				? ToShare()
				: null)
			.Subscribe(x =>
			{
				Share = x;
				// TODO: Try to combine shares.
				IsMnemonicsValid = x is not null;
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);


		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(options),
			canExecute: this.WhenAnyValue(x => x.IsMnemonicsValid));

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(OnAdvancedRecoveryOptionsDialogAsync);
	}

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

	private async Task OnNextAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, _, _) = options;
		ArgumentException.ThrowIfNullOrEmpty(walletName);

		if (Share is not { } share)
		{
			return;
		}

		var shares = _shares is null ? [share] : _shares.Append(share).ToArray();
		var threshold = share.MemberThreshold;

		if (shares.Length < threshold)
		{
			var recoveryWordsBackup = new MultiShareBackup(
				Settings: new MultiShareBackupSettings(Threshold: threshold, Shares: (byte)shares.Length),
				Password: "",
				Shares: shares,
				CurrentShare: (byte) shares.Length);
			options = options with { WalletBackup = recoveryWordsBackup, MinGapLimit = MinGapLimit };

			Navigate().To().RecoverMultiShareWallet(options);
		}
		else
		{
			var password = await Navigate().To().CreatePasswordDialog("Add Passphrase", "If you used a passphrase when you created your wallet you must type it below, otherwise leave this empty.").GetResultAsync();

			if (password is null)
			{
				return;
			}

			IsBusy = true;

			try
			{
				var recoveryWordsBackup = new MultiShareBackup(
					new MultiShareBackupSettings(Threshold: threshold, Shares: (byte)shares.Length),
					password,
					shares);
				options = options with { WalletBackup = recoveryWordsBackup, MinGapLimit = MinGapLimit };
				var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);
				Navigate().To().AddedWalletPage(walletSettings, options);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Wasabi was unable to recover the wallet.");
			}

		}

		IsBusy = false;
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync()
	{
		var result = await Navigate().To().AdvancedRecoveryOptions(MinGapLimit).GetResultAsync();
		if (result is { } minGapLimit)
		{
			MinGapLimit = minGapLimit;
		}
	}

	private void ValidateMnemonics(IValidationErrors errors)
	{
		if (Share is null)
		{
			ClearValidations();
			return;
		}

		if (IsMnemonicsValid)
		{
			return;
		}

		if (!Mnemonics.Any())
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, "Invalid set. Make sure you typed all your recovery words in the correct order.");
	}

	private string GetTagsAsConcatString(IEnumerable<string> tags)
	{
		return string.Join(' ', tags);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		// TODO: Initialize current share mnemonics.

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
