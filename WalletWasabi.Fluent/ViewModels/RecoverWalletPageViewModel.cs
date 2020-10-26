using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.TagsBox;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Suggestions;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
    public class RecoveryPageViewModel : NavBarItemViewModel
    {
        private string _accountKeyPath;

        private int _caretIndex;
        private string _minGapLimit;
        private string _mnemonicWords;
        private string _password;
        private string _walletName;

        public RecoveryPageViewModel(IScreen screen) : base(screen)
        {
            Title = "Recovery";
            RecoveryWordsTagsBox.Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
            RecoveryWordsTagsBox.RestrictInputToSuggestions = true;


            Global = Locator.Current.GetService<Global>();
            WalletManager = Global.WalletManager;

            this.ValidateProperty(x => x.Password, ValidatePassword);
            this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);
            this.ValidateProperty(x => x.AccountKeyPath, ValidateAccountKeyPath);

            // MnemonicWords = "";

            // RecoverCommand = ReactiveCommand.Create(
            //     () => RecoverWallet(),
            //     Observable.FromEventPattern(this, nameof(ErrorsChanged))
            //         .ObserveOn(RxApp.MainThreadScheduler)
            //         .Select(_ => !Validations.AnyErrors));

            // this.WhenAnyValue(x => x.MnemonicWords).Subscribe(UpdateSuggestions);

            _currentMnemonic = RecoveryWordsTagsBox.Tags.ToObservableChangeSet().ToCollection()
                .Select(x => x.Count == 12 ? new Mnemonic(RecoveryWordsTagsBox.GetTagsAsConcatString()) : default)
                .ToProperty(this, x => x.CurrentMnemonics);

            _isMnemonicValid = this.WhenAnyValue(x => x.CurrentMnemonics)
                .Select(x => x?.WordList?.WordCount == 12 && (x?.IsValidChecksum ?? false))
                .ToProperty(this, x => x.IsMnemonicValid);
            //
            // RecoverCommand.ThrownExceptions
            //     .ObserveOn(RxApp.TaskpoolScheduler)
            //     .Subscribe(ex => Logger.LogError(ex));
        }

        private readonly ObservableAsPropertyHelper<Mnemonic?> _currentMnemonic;
        public Mnemonic CurrentMnemonics => _currentMnemonic.Value;

        private readonly ObservableAsPropertyHelper<bool> _isMnemonicValid;
        public bool IsMnemonicValid => _isMnemonicValid.Value;

        public override string IconName => "home_regular";

        public TagsBoxViewModel RecoveryWordsTagsBox { get; } = new TagsBoxViewModel();

        private Global Global { get; }
        private WalletManager WalletManager { get; }

        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        public string WalletName
        {
            get => _walletName;
            set => this.RaiseAndSetIfChanged(ref _walletName, value);
        }

        public string AccountKeyPath
        {
            get => _accountKeyPath;
            set => this.RaiseAndSetIfChanged(ref _accountKeyPath, value);
        }

        public string MinGapLimit
        {
            get => _minGapLimit;
            set => this.RaiseAndSetIfChanged(ref _minGapLimit, value);
        }

        public ReactiveCommand<Unit, Unit> RecoverCommand { get; }
        //
        // private static IEnumerable<string> EnglishWords { get; } = Wordlist.English.GetWords();

        private void RecoverWallet()
        {
            WalletName = Guard.Correct(WalletName);
            Password = Guard.Correct(Password); // Do not let whitespaces to the beginning and to the end.

            var mnemonicWords = Guard.Correct(RecoveryWordsTagsBox.GetTagsAsConcatString());

            string walletFilePath = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

            if (string.IsNullOrWhiteSpace(WalletName))
            {
                NotificationHelpers.Error("Invalid wallet name.");
            }
            else if (File.Exists(walletFilePath))
            {
                NotificationHelpers.Error("Wallet name is already taken.");
            }
            else if (string.IsNullOrWhiteSpace(mnemonicWords))
            {
                NotificationHelpers.Error("Recovery Words were not supplied.");
            }
            else
            {
                var minGapLimit = int.Parse(MinGapLimit);
                var keyPath = KeyPath.Parse(AccountKeyPath);

                try
                {
                    var mnemonic = new Mnemonic(mnemonicWords);

                    var km = KeyManager.Recover(mnemonic, Password, null, keyPath, minGapLimit);
                    km.SetNetwork(Global.Network);
                    km.SetFilePath(walletFilePath);
                    WalletManager.AddWallet(km);

                    NotificationHelpers.Success("Wallet was recovered.");

                    // 
                    //owner.SelectLoadWallet(km);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    NotificationHelpers.Error(ex.ToUserFriendlyString());
                }
            }
        }

        private void ValidatePassword(IValidationErrors errors)
        {
            PasswordHelper.ValidatePassword(errors, Password);
        }

        // public override void OnCategorySelected()
        // {
        //     base.OnCategorySelected();
        //
        //     Password = null;
        //     MnemonicWords = "";
        //
        //     WalletName = WalletManager.WalletDirectories.GetNextWalletName();
        //
        //     AccountKeyPath = $"m/{KeyManager.DefaultAccountKeyPath}";
        //     MinGapLimit = (KeyManager.AbsoluteMinGapLimit * 3).ToString();
        // }

        // private void UpdateSuggestions(string words)
        // {
        //     if (string.IsNullOrWhiteSpace(words))
        //     {
        //         Suggestions?.Clear();
        //         return;
        //     }
        //
        //     string[] enteredWordList = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //     var lastWord = enteredWordList.LastOrDefault().Replace("\t", "");
        //     if (lastWord.Length < 1)
        //     {
        //         Suggestions.Clear();
        //         return;
        //     }
        //
        //     var suggestedWords = EnglishWords.Where(w => w.StartsWith(lastWord)).Take(7);
        //
        //     Suggestions.Clear();
        //     foreach (var suggestion in suggestedWords) Suggestions.Add(new SuggestionViewModel(suggestion, OnAddWord));
        // }

        // public void OnAddWord(string word)
        // {
        //     string[] words = MnemonicWords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //     if (words.Length == 0)
        //     {
        //         MnemonicWords = word + " ";
        //     }
        //     else
        //     {
        //         words[^1] = word;
        //         MnemonicWords = string.Join(' ', words) + " ";
        //     }
        //
        //     CaretIndex = MnemonicWords.Length;
        //
        //     Suggestions.Clear();
        // }

        private void ValidateMinGapLimit(IValidationErrors errors)
        {
            if (!int.TryParse(MinGapLimit, out var minGapLimit) || minGapLimit < KeyManager.AbsoluteMinGapLimit ||
                minGapLimit > KeyManager.MaxGapLimit)
                errors.Add(ErrorSeverity.Error,
                    $"Must be a number between {KeyManager.AbsoluteMinGapLimit} and {KeyManager.MaxGapLimit}.");
        }

        private void ValidateAccountKeyPath(IValidationErrors errors)
        {
            if (string.IsNullOrWhiteSpace(AccountKeyPath))
            {
                errors.Add(ErrorSeverity.Error, "Path is not valid.");
            }
            else if (KeyPath.TryParse(AccountKeyPath, out var keyPath))
            {
                var accountKeyPath = keyPath.GetAccountKeyPath();
                if (keyPath.Length != accountKeyPath.Length ||
                    accountKeyPath.Length != KeyManager.DefaultAccountKeyPath.Length)
                    errors.Add(ErrorSeverity.Error, "Path is not a compatible account derivation path.");
            }
            else
            {
                errors.Add(ErrorSeverity.Error, "Path is not a valid derivation path.");
            }
        }
    }
}