using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(Title = "WalletVerifyRecoveryWordsViewModel_Title")]
public partial class WalletVerifyRecoveryWordsViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;

	private WalletVerifyRecoveryWordsViewModel(IWalletModel wallet)
	{
		_suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(wallet), this.WhenAnyValue(x => x.CurrentMnemonics).Select(x => x is not null));

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableAutoBusyOn(NextCommand);
	}

	public ObservableCollection<string> Mnemonics { get; } = new();

	private bool IsMnemonicsValid => CurrentMnemonics is { IsValidChecksum: true };

	private async Task ShowErrorAsync()
	{
		await ShowErrorAsync(
			Lang.Resources.WalletVerifyRecoveryWordsViewModel_Error_Incorrect_Title,
			Lang.Resources.WalletVerifyRecoveryWordsViewModel_Error_Incorrect_Message,
			Lang.Resources.WalletVerifyRecoveryWordsViewModel_Error_Incorrect_Caption);
	}

	private async Task OnNextAsync(IWalletModel wallet)
	{
		try
		{
			if (!IsMnemonicsValid || CurrentMnemonics is not { } currentMnemonics)
			{
				await ShowErrorAsync();

				Mnemonics.Clear();
				return;
			}

			var verificationResult = wallet.Auth.VerifyRecoveryWords(currentMnemonics);
			if (verificationResult)
			{
				Navigate().To().Success(navigationMode: NavigationMode.Clear);
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
			await ShowErrorAsync(
				Lang.Resources.WalletVerifyRecoveryWordsViewModel_Title,
				ex.ToUserFriendlyString(),
				Lang.Resources.WalletVerifyRecoveryWordsViewModel_Error_Generic_Caption);
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

		errors.Add(ErrorSeverity.Error, Lang.Resources.WalletVerifyRecoveryWordsViewModel_Error_Invalid);
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}
}
