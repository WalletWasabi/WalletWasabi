using System.Collections;
using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.TagsBox;

namespace WalletWasabi.Fluent.ViewModels
{
	public class RecoveryPageViewModel : NavBarItemViewModel
	{
		public RecoveryPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Recovery";
			RecoveryWordsTagsBox.Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
			RecoveryWordsTagsBox.RestrictInputToSuggestions = true;
		}

		public override string IconName => "home_regular";

		public TagsBoxViewModel RecoveryWordsTagsBox { get; } = new TagsBoxViewModel();
	}
}
