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
	[AutoNotify] private Mnemonic? _currentMnemonics;
	private readonly Wallet _wallet;

	public VerifyRecoveryWordsViewModel(Wallet wallet)
	{
		_suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		_wallet = wallet;

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x =>
				x.Count is 12 or 15 or 18 or 21 or 24
					? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant())
					: default)
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
			async () => await OnNextAsync());

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private async Task ShowErrorAsync()
	{
		await ShowErrorAsync("Error",
			"Try again, but if you are unable to verify your Recovery Words, you MUST move your funds to a new wallet as soon as possible.",
			"The Recovery Words you entered were incorrect.");
	}

	private async Task OnNextAsync()
	{
		IsBusy = true;

		try
		{
			if (!IsMnemonicsValid)
			{
				await ShowErrorAsync();

				Mnemonics.Clear();
				IsBusy = false;
				return;
			}

			if (_currentMnemonics is { })
			{
				var saltSoup = _wallet.Kitchen.SaltSoup();

				var recovered = KeyManager.Recover(_currentMnemonics, saltSoup, _wallet.Network,
					_wallet.KeyManager.AccountKeyPath,
					null, _wallet.KeyManager.MinGapLimit);

				if (_wallet.KeyManager.ExtPubKey == recovered.ExtPubKey)
				{
					Navigate().To(new SuccessViewModel("Your Recovery Words have been verified and are correct."),
						NavigationMode.Clear);
				}
				else
				{
					await ShowErrorAsync();

					Mnemonics.Clear();
				}
			}
		}
		catch (Exception ex)
		{
			// TODO navigate to error dialog.
			Logger.LogError(ex);
		}

		IsBusy = false;
	}

	public bool IsMnemonicsValid => CurrentMnemonics is { IsValidChecksum: true };

	public IObservable<bool> NextCommandCanExecute { get; }

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
}
