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
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoverWalletViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	[AutoNotify] private bool _isMnemonicsValid;

	private RecoverWalletViewModel(string walletName)
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

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(OnAdvancedRecoveryOptionsDialogAsync);
	}

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

	private async Task OnNextAsync(string walletName)
	{
		var password = await Navigate().To().CreatePasswordDialog("Add Password", "Type the password of the wallet if there is one").GetResultAsync();
		if (password is not { } || CurrentMnemonics is not { IsValidChecksum: true } currentMnemonics)
		{
			return;
		}

		IsBusy = true;

		try
		{
			var wallet = await UiContext.WalletRepository.RecoverWalletAsync(walletName, password, currentMnemonics, MinGapLimit);
			await Navigate().To().CoinJoinProfiles(wallet).GetResultAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Wasabi was unable to recover the wallet.");
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

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
