using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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

		public RecoverWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network,
			WalletManager walletManager) : base(navigationState, NavigationTarget.Dialog)
		{
			Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

			Mnemonics.ToObservableChangeSet().ToCollection()
				.Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString()) : default)
				.Subscribe(x => CurrentMnemonics = x);

			this.WhenAnyValue(x => x.SelectedTag)
				.Where(x => !string.IsNullOrEmpty(x))
				.Subscribe(AddMnemonic);

			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Subscribe(x => this.RaisePropertyChanged(nameof(Mnemonics)));

			this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

			AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var (accountKeyPathIn, minGapLimitIn) = await AdvancedOptionsInteraction
						.Handle((AccountKeyPath!, (int) MinGapLimit!)).ToTask();

					if (accountKeyPathIn is { })
					{
						AccountKeyPath = accountKeyPathIn;
					}

					if (minGapLimitIn is { })
					{
						MinGapLimit = minGapLimitIn;
					}
				});

			FinishCommandCanExecute = this.WhenAnyValue(
				x => x.CurrentMnemonics,
				x => x.AccountKeyPath,
				x => x.MinGapLimit,
				(_, __, ___) =>
					AccountKeyPath is { } &&
					MinGapLimit is { } &&
					CurrentMnemonics is { } &&
					(CurrentMnemonics?.IsValidChecksum ?? false) &&
					!Validations.Any);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					try
					{
						var password = await PasswordInteraction
							.Handle("Type the password of the wallet to be to recover and click Continue.").ToTask();

						if (string.IsNullOrEmpty(password))
						{
							return;
						}

						var walletFilePath = walletManager.WalletDirectories.GetWalletFilePaths(walletName)
							.walletFilePath;
						var keyManager = KeyManager.Recover(
							CurrentMnemonics!,
							password,
							walletFilePath,
							AccountKeyPath!,
							(int) MinGapLimit!);
						keyManager.SetNetwork(network);
						walletManager.AddWallet(keyManager);

						navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear();
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				},
				FinishCommandCanExecute);

			AdvancedOptionsInteraction = new Interaction<(KeyPath, int), (KeyPath?, int?)>();

			AdvancedOptionsInteraction.RegisterHandler(
				async interaction =>
					interaction.SetOutput(
						await new AdvancedRecoveryOptionsViewModel(interaction.Input).ShowDialogAsync()));

			PasswordInteraction = new Interaction<string, string?>();

			PasswordInteraction.RegisterHandler(
				async interaction =>
					interaction.SetOutput(await new EnterPasswordViewModel(interaction.Input).ShowDialogAsync()));
		}

		public IObservable<bool> FinishCommandCanExecute { get; }

		public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

		public ICommand NextCommand { get; }

		private KeyPath? AccountKeyPath { get; set; } = KeyPath.Parse("m/84'/0'/0'");

		private int? MinGapLimit { get; set; } = 63;

		private Interaction<(KeyPath, int), (KeyPath?, int?)> AdvancedOptionsInteraction { get; }

		private Interaction<string, string?> PasswordInteraction { get; }

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
