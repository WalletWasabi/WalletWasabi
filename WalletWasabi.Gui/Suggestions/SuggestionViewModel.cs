using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Gui.Suggestions
{
	public class SuggestionViewModel : ReactiveObject
	{
		private bool _isHighLighted;

		public SuggestionViewModel(string word, Action<string> onSeleted)
		{
			Word = word;
			OnSelection = onSeleted;
		}

		public string Word { get; }
		public Action<string> OnSelection { get; }

		public bool IsHighLighted
		{
			get => _isHighLighted;
			set => this.RaiseAndSetIfChanged(ref _isHighLighted, value);
		}

		public void OnSelected()
		{
			OnSelection?.Invoke(Word);
		}
	}
}
