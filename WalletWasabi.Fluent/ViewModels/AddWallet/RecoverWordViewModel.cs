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
	[AutoNotify] private IReadOnlyCollection<string>? _suggestions;

	public RecoverWordViewModel(int index, string word, IReadOnlyCollection<string>? suggestions)
	{
		Index = index;

		_word = word;

		// TODO:
		IsConfirmed = true;

		Suggestions = suggestions;

		this.WhenAnyValue(x => x.SelectedWord)
			.Subscribe(_ => ValidateWord());
	}

	public int Index { get; }

	private void ValidateWord()
	{
		// TODO:
	}

	public override string ToString()
	{
		return $"{Index}. {Word}";
	}
}
