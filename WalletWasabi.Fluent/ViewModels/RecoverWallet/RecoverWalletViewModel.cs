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
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.RecoverWallet
{
    public class RecoverWalletViewModel : RoutableViewModel
    {
        private readonly ObservableAsPropertyHelper<Mnemonic?> _currentMnemonic;
        private string? _selectedTag;
        private IEnumerable<string>? _suggestions;

        public RecoverWalletViewModel(IScreen screen, string walletName, Network network, string password,
            WalletManager walletManager) :
            base(screen)
        {
            Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

            _currentMnemonic = Mnemonics.ToObservableChangeSet().ToCollection()
                .Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString()) : default)
                .ToProperty(this, x => x.CurrentMnemonics);

            this.WhenAnyValue(x => x.SelectedTag)
                .Where(x => !string.IsNullOrEmpty(x))
                .Subscribe(AddMnemonic);

            this.WhenAnyValue(x => x.CurrentMnemonics)
                .Subscribe(x => this.RaisePropertyChanged(nameof(Mnemonics)));

            this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

            AdvancedOptionsInteraction = new Interaction<object, (KeyPath?, int?)>();
            AdvancedOptionsInteraction.RegisterHandler(
                async interaction =>
                    interaction.SetOutput(await new AdvancedRecoveryOptionsViewModel().ShowDialogAsync()));

            AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    var (accountKeyPathIn, minGapLimitIn) = await AdvancedOptionsInteraction.Handle("").ToTask();

                    if (accountKeyPathIn is { })
                        AccountKeyPath = accountKeyPathIn;

                    if (minGapLimitIn is { })
                        MinGapLimit = minGapLimitIn;
                });

            var finishCommandCanExecute = this.WhenAnyValue(
                    x => x.CurrentMnemonics,
                    x => x.AccountKeyPath,
                    x => x.MinGapLimit,
                    delegate
                    {
                        // This will fire validations before return canExecute value.
                        this.RaisePropertyChanged(nameof(CurrentMnemonics));
                        this.RaisePropertyChanged(nameof(AccountKeyPath));
                        this.RaisePropertyChanged(nameof(MinGapLimit));

                        return CurrentMnemonics is { } && (CurrentMnemonics?.IsValidChecksum ?? false) &&
                               !Validations.Any;
                    })
                .ObserveOn(RxApp.MainThreadScheduler);

            FinishCommand = ReactiveCommand.Create(() =>
            {
                try
                {
                    if (CurrentMnemonics is null || AccountKeyPath is null || MinGapLimit is null) return;
                    var walletFilePath = walletManager.WalletDirectories.GetWalletFilePaths(walletName).walletFilePath;
                    var keyManager = KeyManager.Recover(CurrentMnemonics, password, walletFilePath, AccountKeyPath,
                        (int) MinGapLimit);
                    keyManager.SetNetwork(network);
                    walletManager.AddWallet(keyManager);
                    screen.Router.NavigationStack.Clear();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
                
            }, finishCommandCanExecute);

            CancelCommand = ReactiveCommand.Create(() =>
            {
                password = "";
                AccountKeyPath = default;
                MinGapLimit = default;
            });
        }

        public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

        public ICommand FinishCommand { get; }

        public ICommand CancelCommand { get; }

        private KeyPath? AccountKeyPath { get; set; } = KeyPath.Parse("m/84'/0'/0'");

        private int? MinGapLimit { get; set; } = 63;

        private Interaction<object, (KeyPath?, int?)> AdvancedOptionsInteraction { get; }

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

        private Mnemonic? CurrentMnemonics => _currentMnemonic.Value;

        private void ValidateMnemonics(IValidationErrors errors)
        {
            if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
                errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
        }

        private void AddMnemonic(string? tagString)
        {
            if (!string.IsNullOrWhiteSpace(tagString)) Mnemonics.Add(tagString);

            SelectedTag = string.Empty;
        }

        private string GetTagsAsConcatString()
        {
            return string.Join(' ', Mnemonics);
        }
    }
}