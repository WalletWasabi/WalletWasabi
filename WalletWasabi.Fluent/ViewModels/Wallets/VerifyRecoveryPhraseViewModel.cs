using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Recovery Words Verification")]
public partial class VerifyRecoveryPhraseViewModel : RoutableViewModel
{
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	private readonly Wallet _wallet;

	public VerifyRecoveryPhraseViewModel(Wallet wallet)
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
			async () => await OnNextAsync(),
			NextCommandCanExecute);
	}

	private async Task OnNextAsync()
	{
		var dialogResult = await NavigateDialogAsync(
			new PasswordAuthDialogViewModel(_wallet),
			NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result)
		{
			IsBusy = true;

			try
			{
				if (_currentMnemonics is { })
				{
					var saltSoup = _wallet.Kitchen.SaltSoup();

					var recovered = KeyManager.Recover(_currentMnemonics, saltSoup, _wallet.Network, _wallet.KeyManager.AccountKeyPath,
						null, _wallet.KeyManager.MinGapLimit);

					if (_wallet.KeyManager.ExtPubKey == recovered.ExtPubKey)
					{
						Navigate().To(new SuccessViewModel("Your Seed Recovery words have been verified as correct."),
							NavigationMode.Clear);
					}
					else
					{
						await ShowErrorAsync("Error", "Your recovery phrase was incorrect.",
							"You may try again, but if you are unable to verify your recovery phrase correctly. You MUST move your funds to a new wallet as soon as possible.");

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
		else
		{
			await ShowErrorAsync("Error", "The entered password was incorrect.",
				"You may try again, but if you are unable to verify your recovery phrase correctly. You MUST move your funds to a new wallet as soon as possible.");
		}
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

		errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}
}
