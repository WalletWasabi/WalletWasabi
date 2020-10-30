using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
    public class RecoveryPageViewModel : NavBarItemViewModel
    {
        private readonly ObservableAsPropertyHelper<Mnemonic?> _currentMnemonic;
        private readonly ObservableAsPropertyHelper<bool> _isMnemonicValid;
        private ObservableCollection<string> _mnemonics;
        private IEnumerable _suggestions;
        private int _itemCountLimit;
        private bool _restrictInputToSuggestions;

        public ObservableCollection<string> Mnemonics
        {
            get => _mnemonics;
            set => this.RaiseAndSetIfChanged(ref _mnemonics, value);
        }

        public IEnumerable Suggestions
        {
            get => _suggestions;
            set => this.RaiseAndSetIfChanged(ref _suggestions, value);
        } 

        public int ItemCountLimit
        {
            get => _itemCountLimit;
            set => this.RaiseAndSetIfChanged(ref _itemCountLimit, value);
        }

        public bool RestrictInputToSuggestions
        {
            get => _restrictInputToSuggestions;
            set => this.RaiseAndSetIfChanged(ref _restrictInputToSuggestions, value);
        }

        public RecoveryPageViewModel(IScreen screen) : base(screen)
        {
            Global = Locator.Current.GetService<Global>();

            Title = "Recovery";

            Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
            RestrictInputToSuggestions = true;
            ItemCountLimit = (int)WordCount.Twelve;
            
            Mnemonics = new ObservableCollection<string>();

            _currentMnemonic = Mnemonics.ToObservableChangeSet().ToCollection()
                .Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString()) : default)
                .ToProperty(this, x => x.CurrentMnemonics);

            _isMnemonicValid = this.WhenAnyValue(x => x.CurrentMnemonics)
                .Select(x => x?.WordList?.WordCount == 12 && (x?.IsValidChecksum ?? false))
                .ToProperty(this, x => x.IsMnemonicValid);

            this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

            // A hack for validations system...
            Mnemonics.CollectionChanged += MnemonicsChanged;
        }

        // ugly hack
        private void MnemonicsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(Mnemonics));
        }

        private void ValidateMnemonics(IValidationErrors errors)
        {
            // // example code only
            // if (Mnemonics?.Contains("machine") ?? false)
            // {
                errors.Add(ErrorSeverity.Error, "Example Error");
            // }
        }

        public string GetTagsAsConcatString()
        {
            return string.Join(' ', Mnemonics);
        }

        public Mnemonic? CurrentMnemonics => _currentMnemonic?.Value;
        public bool IsMnemonicValid => _isMnemonicValid.Value;
        public override string IconName => "home_regular";
        private Global Global { get; }
    }
}