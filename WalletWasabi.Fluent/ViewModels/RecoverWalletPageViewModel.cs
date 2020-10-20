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
			RecoveryWordsTagBox.Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
		}

		public override string IconName => "home_regular";

		public TagBoxViewModel RecoveryWordsTagBox { get; } = new TagBoxViewModel();
	}
}
