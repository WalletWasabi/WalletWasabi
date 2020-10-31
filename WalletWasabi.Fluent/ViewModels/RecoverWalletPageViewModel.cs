using System;
using System.Collections;
using System.Collections.Generic;
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
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels
{
	public class RecoveryPageViewModel : NavBarItemViewModel
	{
		private readonly ObservableAsPropertyHelper<Mnemonic?> _currentMnemonic;
		private ObservableCollection<string> _mnemonics;
		private IEnumerable<string> _suggestions;
		private string _selectedTag;

		public ObservableCollection<string> Mnemonics
		{
			get => _mnemonics;
			set => this.RaiseAndSetIfChanged(ref _mnemonics, value);
		}

		public IEnumerable<string> Suggestions
		{
			get => _suggestions;
			set => this.RaiseAndSetIfChanged(ref _suggestions, value);
		}

		public string SelectedTag
		{
			get => _selectedTag;
			set => this.RaiseAndSetIfChanged(ref _selectedTag, value);
		}

		public RecoveryPageViewModel(IScreen screen) : base(screen)
		{
			Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
			Mnemonics = new ObservableCollection<string>();

			_currentMnemonic = Mnemonics.ToObservableChangeSet().ToCollection()
				.Select(x => x.Count == 12 ? new Mnemonic(GetTagsAsConcatString()) : default)
				.ToProperty(this, x => x.CurrentMnemonics);

			this.WhenAnyValue(x => x.SelectedTag)
				.Where(x => !string.IsNullOrEmpty(x))
				.Subscribe(AddTag);

			// TODO: Will try to find ways of doing this better...
			this.WhenAnyValue(x => x.CurrentMnemonics)
				.Subscribe(x => this.RaisePropertyChanged(nameof(Mnemonics)));

			this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);
		}

		private void ValidateMnemonics(IValidationErrors errors)
		{
			if (CurrentMnemonics is { } && !CurrentMnemonics.IsValidChecksum)
			{
				errors.Add(ErrorSeverity.Error, "Invalid recovery seed phrase.");
			}
		}

		private void AddTag(string tagString)
		{
			Mnemonics.Add(tagString);
			SelectedTag = string.Empty;
		}

		private string GetTagsAsConcatString()
		{
			return string.Join(' ', Mnemonics);
		}

		public Mnemonic? CurrentMnemonics => _currentMnemonic.Value;
		public override string IconName => "settings_regular";
	}
}