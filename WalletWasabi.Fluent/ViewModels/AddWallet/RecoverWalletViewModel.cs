using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Enter recovery words")]
	public partial class RecoverWalletViewModel : RoutableViewModel
	{
		[AutoNotify] private IEnumerable<string>? _suggestions;
		[AutoNotify] private Mnemonic? _currentMnemonics;

		public RecoverWalletViewModel(
			string walletName,
			WalletManagerViewModel walletManagerViewModel)
		{
			Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
			var walletManager = walletManagerViewModel.WalletManager;
			var network = walletManager.Network;

			Mnemonics.ToObservableChangeSet().ToCollection()
				.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : default)
				.Subscribe(x => CurrentMnemonics = x);

			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Mnemonics)));

			this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

			EnableCancel = true;

			EnableBack = true;

			NextCommandCanExecute =
				this.WhenAnyValue(x => x.CurrentMnemonics)
					.Select(currentMnemonics => currentMnemonics is { } && !Validations.Any);

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNext(walletManager, network, walletName),
				NextCommandCanExecute);

			AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
				async () => await OnAdvancedRecoveryOptionsDialog());

			EnableAutoBusyOn(NextCommand);
		}

		private async Task OnNext(
			WalletManager walletManager,
			Network network,
			string? walletName)
		{
			var dialogResult = await NavigateDialog(
				new CreatePasswordDialogViewModel(
					"Type the password of the wallet to be able to recover and click Continue."));

			if (dialogResult.Result is { } password)
			{
				try
				{
					var keyManager = await Task.Run(
						() =>
						{
							var walletFilePath = walletManager.WalletDirectories.GetWalletFilePaths(walletName!)
								.walletFilePath;

							var result = KeyManager.Recover(
								CurrentMnemonics!,
								password!,
								walletFilePath,
								AccountKeyPath,
								MinGapLimit);

							result.SetNetwork(network);

							return result;
						});

					Navigate().To(new AddedWalletPageViewModel(walletManager, keyManager));

					return;
				}
				catch (Exception ex)
				{
					// TODO navigate to error dialog.
					Logger.LogError(ex);
				}
			}

			if (dialogResult.Kind == DialogResultKind.Cancel)
			{
				Navigate().Clear();
			}
		}

		private async Task OnAdvancedRecoveryOptionsDialog()
		{
			var result = await NavigateDialog(new AdvancedRecoveryOptionsViewModel((AccountKeyPath, MinGapLimit)),
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

		public IObservable<bool> NextCommandCanExecute { get; }

		public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

		private KeyPath AccountKeyPath { get; set; } = KeyPath.Parse("m/84'/0'/0'");

		private int MinGapLimit { get; set; } = 63;

		public ObservableCollection<string> Mnemonics { get; } = new();

		private void ValidateMnemonics(IValidationErrors errors)
		{
			if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
			{
				errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
			}
		}

		private string GetTagsAsConcatString()
		{
			return string.Join(' ', Mnemonics);
		}
	}
}
