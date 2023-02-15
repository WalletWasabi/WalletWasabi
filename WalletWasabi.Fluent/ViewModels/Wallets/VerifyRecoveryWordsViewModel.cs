using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Verify Recovery Words")]
public partial class VerifyRecoveryWordsViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private bool _isMnemonicsValid;
	private Mnemonic? _currentMnemonics;

	public VerifyRecoveryWordsViewModel(Wallet wallet)
	{
		_suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
			.Subscribe(x =>
			{
				_currentMnemonics = x;
				IsMnemonicsValid = x is { IsValidChecksum: true };
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(wallet));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableAutoBusyOn(NextCommand);
	}

	public ObservableCollection<string> Mnemonics { get; } = new();

	private async Task ShowErrorAsync()
	{
		await ShowErrorAsync(
			"Error",
			"Try again, but if you are unable to verify your Recovery Words, you MUST move your funds to a new wallet as soon as possible.",
			"The Recovery Words you entered were incorrect.");
	}

	private async Task OnNextAsync(Wallet wallet)
	{
		try
		{
			if (!IsMnemonicsValid || _currentMnemonics is not { } currentMnemonics)
			{
				await ShowErrorAsync();

				Mnemonics.Clear();
				return;
			}

			var saltSoup = wallet.Kitchen.SaltSoup();

			var recovered = KeyManager.Recover(
				currentMnemonics,
				saltSoup,
				wallet.Network,
				wallet.KeyManager.SegwitAccountKeyPath,
				null,
				null,
				wallet.KeyManager.MinGapLimit);

			if (wallet.KeyManager.SegwitExtPubKey == recovered.SegwitExtPubKey)
			{
				Navigate().To(new SuccessViewModel("Your Recovery Words have been verified and are correct."), NavigationMode.Clear);
			}
			else
			{
				await ShowErrorAsync();
				Mnemonics.Clear();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.Message, "Wasabi was unable to verify the recovery words.");
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
}
