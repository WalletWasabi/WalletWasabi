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

	public RecoverWalletViewModel(string walletName)
	{
		Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommandCanExecute =
			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Select(_ => IsMnemonicsValid);

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(walletName),
			NextCommandCanExecute);

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
			async () => await OnAdvancedRecoveryOptionsDialogAsync());
	}

	private async Task OnNextAsync(string? walletName)
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Add Password", "Type the password of the wallet if there is one")
			, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } password)
		{
			IsBusy = true;

			try
			{
				var keyManager = await Task.Run(
					() =>
					{
						var walletFilePath = Services.WalletManager.WalletDirectories.GetWalletFilePaths(walletName!)
						   .walletFilePath;

						var result = KeyManager.Recover(
							CurrentMnemonics!,
							password!,
							Services.WalletManager.Network,
							AccountKeyPath,
							"", // Make sure it is not saved into a file yet.
							MinGapLimit);

						result.AutoCoinJoin = true;
						result.SetNetwork(Services.WalletManager.Network);

						// Set the filepath but we will only write the file later when the Ui workflow is done.
						result.SetFilePath(walletFilePath);

						return result;
					});

				await NavigateDialogAsync(new CoinJoinProfilesViewModel(keyManager, isNewWallet: true));
			}
			catch (Exception ex)
			{
				// TODO navigate to error dialog.
				Logger.LogError(ex);
			}

			IsBusy = false;
		}
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync()
	{
		var result = await NavigateDialogAsync(new AdvancedRecoveryOptionsViewModel((AccountKeyPath, MinGapLimit)),
			NavigationTarget.CompactDialogScreen);

		if (result.Kind == DialogResultKind.Normal)
		{
			var (accountKeyPathIn, minGapLimitIn) = result.Result;

			if (accountKeyPathIn is { } && minGapLimitIn is { })
			{
				AccountKeyPath = accountKeyPathIn;
				MinGapLimit = (int)minGapLimitIn;
			}
		}
	}

	public bool IsMnemonicsValid => CurrentMnemonics is { IsValidChecksum: true };

	public IObservable<bool> NextCommandCanExecute { get; }

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private KeyPath AccountKeyPath { get; set; } = KeyManager.GetAccountKeyPath(Services.WalletManager.Network);

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

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
