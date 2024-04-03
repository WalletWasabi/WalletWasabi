using System.Collections.Generic;
using System.Collections.ObjectModel;
using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

public partial class RecoverWordViewModel : ViewModelBase
{
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private bool _isEnabled;
	[AutoNotify] private string? _selectedWord;
	[AutoNotify] private string _word;
	[AutoNotify] private bool _isMnemonicsValid;
	[AutoNotify] private IEnumerable<string>? _suggestions;

	public RecoverWordViewModel(int index, string word)
	{
		Index = index;
		Word = word;

		// TODO:
		IsConfirmed = true;

		Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		this.WhenAnyValue(x => x.SelectedWord)
			.Subscribe(_ => ValidateWord());
	}

	public int Index { get; }

	public ObservableCollection<string> Mnemonics { get; } = new();

	private void ValidateWord()
	{
		// TODO:
		// IsConfirmed = Word.Equals(SelectedWord, StringComparison.InvariantCultureIgnoreCase);
	}

	public override string ToString()
	{
		return $"{Index}. {Word}";
	}
}
