using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoverWalletViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	[AutoNotify] private bool _isMnemonicsValid;

	public RecoverWalletViewModel(string walletName)
	{
		Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				IsMnemonicsValid = x is { IsValidChecksum: true };
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(walletName),
			canExecute: this.WhenAnyValue(x => x.IsMnemonicsValid));

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
			async () => await OnAdvancedRecoveryOptionsDialogAsync());
	}

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private KeyPath AccountKeyPath { get; } = KeyManager.GetAccountKeyPath(Services.WalletManager.Network, ScriptPubKeyType.Segwit);

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

	private async Task OnNextAsync(string walletName)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Add Password", "Type the password of the wallet if there is one"),
			NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is not { } password || CurrentMnemonics is not { IsValidChecksum: true } currentMnemonics)
		{
			return;
		}

		IsBusy = true;

		try
		{
			var keyManager = await Task.Run(() =>
				{
					var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;

					var result = KeyManager.Recover(
						currentMnemonics,
						password,
						Services.WalletManager.Network,
						AccountKeyPath,
						null,
						"", // Make sure it is not saved into a file yet.
						MinGapLimit);

					result.AutoCoinJoin = true;

					// Set the filepath but we will only write the file later when the Ui workflow is done.
					result.SetFilePath(walletFilePath);

					return result;
				});

			await NavigateDialogAsync(new CoinJoinProfilesViewModel(keyManager, isNewWallet: true));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.Message, "Wasabi was unable to recover the wallet.");
		}

		IsBusy = false;
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync()
	{
		var result = await NavigateDialogAsync(
			new AdvancedRecoveryOptionsViewModel(MinGapLimit),
			NavigationTarget.CompactDialogScreen);

		if (result.Kind == DialogResultKind.Normal && result.Result is { } minGapLimit)
		{
			MinGapLimit = minGapLimit;
		}
	}

	private void ValidateMnemonics(IValidationErrors errors)
	{
		if (IsMnemonicsValid)
		{
			return;
		}

		if (!Mnemonics.Any())
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, "Recovery Words are not valid.");
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
