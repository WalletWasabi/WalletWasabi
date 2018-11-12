using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	public class SuggestionViewModel 
	{
		public string Word { get; }
		public Action<string> OnSelection { get; }

		public SuggestionViewModel(string word, Action<string> onSeleted)
		{
			Word = word;
			OnSelection = onSeleted;
		}

		public void OnSelected()
		{
			OnSelection(Word);
		}
	}
}