using System.Collections;
using System.Collections.Generic;
using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels
{
	public class RecoveryPageViewModel : NavBarItemViewModel
	{
		public RecoveryPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Recovery";
		}

		public override string IconName => "home_regular";

		public IEnumerable MnemonicSuggestions => new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();
	}
}
