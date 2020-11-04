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
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.RecoverWallet
{
    public class RecoverWalletViewModel : RoutableViewModel
    {
        private readonly ObservableAsPropertyHelper<Mnemonic?> _currentMnemonic;
        private IEnumerable<string>? _suggestions;
        private string? _selectedTag;

        public RecoverWalletViewModel(IScreen screen, string walletName, string password, WalletManager walletManager) : base(screen)
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
            
            AdvancedOptionsInteraction = new Interaction<object, (string?, string?)>();
            AdvancedOptionsInteraction.RegisterHandler(
                async interaction =>
                    interaction.SetOutput(await new AdvancedRecoveryOptionsViewModel().ShowDialogAsync()));

            AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    var result = await AdvancedOptionsInteraction.Handle("").ToTask();
                });
        } 
        public ICommand AdvancedRecoveryOptionsDialogCommand { get; }
        
        private Interaction<object, (string?, string?)> AdvancedOptionsInteraction { get; }
        
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

        private void ValidateMnemonics(IValidationErrors errors)
        {
            if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
            {
                errors.Add(ErrorSeverity.Error, "Recovery words are not valid.");
            }
        }

        private void AddMnemonic(string? tagString)
        {
            if (!string.IsNullOrWhiteSpace(tagString))
            {
                Mnemonics.Add(tagString);
            }

            SelectedTag = string.Empty;
        }

        private string GetTagsAsConcatString()
        {
            return string.Join(' ', Mnemonics);
        }

        private Mnemonic? CurrentMnemonics => _currentMnemonic.Value;
    }
}