using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class RecoverWalletViewModel : RoutableViewModel
	{
		private string? _selectedTag;
		private IEnumerable<string>? _suggestions;
		private Mnemonic? _currentMnemonics;
		private bool _isBusy;

		public RecoverWalletViewModel(
			NavigationStateViewModel navigationState,
			string walletName,
			Network network,
			WalletManager walletManager) : base(navigationState, NavigationTarget.DialogScreen)
		{
			Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

			Mnemonics.ToObservableChangeSet().ToCollection()
				.Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString()) : default)
				.Subscribe(x => CurrentMnemonics = x);

			this.WhenAnyValue(x => x.SelectedTag)
				.Where(x => !string.IsNullOrEmpty(x))
				.Subscribe(AddMnemonic);

			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Mnemonics)));

			this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

			FinishCommandCanExecute =
				this.WhenAnyValue(x => x.CurrentMnemonics)
					.Select(currentMnemonics => currentMnemonics is { } && !Validations.Any);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNext(navigationState, walletManager, network, walletName),
				FinishCommandCanExecute);

			AdvancedOptionsInteraction = new Interaction<(KeyPath, int), (KeyPath?, int?)>();
			AdvancedOptionsInteraction.RegisterHandler(
				async interaction =>
					interaction.SetOutput(
						await new AdvancedRecoveryOptionsViewModel(navigationState, NavigationTarget.DialogHost,
							interaction.Input).ShowDialogAsync()));

			AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var (accountKeyPathIn, minGapLimitIn) = await AdvancedOptionsInteraction
						.Handle((AccountKeyPath, MinGapLimit)).ToTask();

					if (accountKeyPathIn is { } && minGapLimitIn is { })
					{
						AccountKeyPath = accountKeyPathIn;
						MinGapLimit = (int) minGapLimitIn;
					}
				});

		}

		private async Task OnNext(NavigationStateViewModel navigationState, WalletManager walletManager,
			Network network, string? walletName)
		{
			IsBusy = true;

			try
			{
				var enterPassword = new EnterPasswordViewModel(
					navigationState,
					NavigationTarget.DialogScreen,
					"Type the password of the wallet to be able to recover and click Continue.");

				NavigateTo(enterPassword, NavigationTarget.DialogScreen);

				var result = await enterPassword.GetDialogResultAsync();

				NavigateToSelf();

				if (result is { } password)
				{
					await Task.Run(() =>
					{
						var walletFilePath = walletManager.WalletDirectories.GetWalletFilePaths(walletName!)
							.walletFilePath;

						var keyManager = KeyManager.Recover(
							CurrentMnemonics!,
							password!,
							walletFilePath,
							AccountKeyPath,
							MinGapLimit);

						keyManager.SetNetwork(network);

						walletManager.AddWallet(keyManager);
					});
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
			finally
			{
				ClearNavigation(NavigationTarget.DialogScreen);
				IsBusy = false;
			}
		}

		public IObservable<bool> FinishCommandCanExecute { get; }

		public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

		private KeyPath AccountKeyPath { get; set; } = KeyPath.Parse("m/84'/0'/0'");

		private int MinGapLimit { get; set; } = 63;

		private Interaction<(KeyPath, int), (KeyPath?, int?)> AdvancedOptionsInteraction { get; }

		public ObservableCollection<string> Mnemonics { get; } = new ObservableCollection<string>();

		public IEnumerable<string>? Suggestions
		{
			get => _suggestions;
			set => this.RaiseAndSetIfChanged(ref _suggestions, value);
		}

		public string? SelectedTag
		{
			get => _selectedTag;
			set => this.RaiseAndSetIfChanged(ref _selectedTag, value);
		}

		private Mnemonic? CurrentMnemonics
		{
			get => _currentMnemonics;
			set => this.RaiseAndSetIfChanged(ref _currentMnemonics, value);
		}

		private bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		private void ValidateMnemonics(IValidationErrors errors)
		{
			if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
			{
				errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
			}
		}

		private void AddMnemonic(string? tagString)
		{
			if (!string.IsNullOrWhiteSpace(tagString) && Mnemonics.Count + 1 <= 12)
			{
				Mnemonics.Add(tagString);
			}

			SelectedTag = string.Empty;
		}

		private string GetTagsAsConcatString()
		{
			return string.Join(' ', Mnemonics);
		}
	}
}