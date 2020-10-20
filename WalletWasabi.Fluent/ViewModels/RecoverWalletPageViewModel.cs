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
		}

		public override string IconName => "home_regular";

		public TagBoxViewModel RecoveryWordsTagBox { get; } = new TagBoxViewModel();
		
		public IEnumerable MnemonicSuggestions { get; } = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
	}
}
