using ReactiveUI;
using System;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
    public class SuggestionViewModel : ReactiveObject
	{
		public string Word { get; }
		public Action<string> OnSelection { get; }

		public bool _isHighLighted;

		public SuggestionViewModel(string word, Action<string> onSeleted)
		{
			Word = word;
			OnSelection = onSeleted;
		}

		public void OnSelected()
		{
			OnSelection?.Invoke(Word);
		}

		public bool IsHighLighted
		{
			get => _isHighLighted;
			set => this.RaiseAndSetIfChanged(ref _isHighLighted, value);
		}
	}
}
