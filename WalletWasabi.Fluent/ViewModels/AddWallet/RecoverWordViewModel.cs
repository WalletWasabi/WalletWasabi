using System.Collections.Generic;
using System.Collections.ObjectModel;
using NBitcoin;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

public partial class RecoverWordViewModel : ViewModelBase
{
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private string? _word;
	[AutoNotify] private IReadOnlyCollection<string>? _suggestions;

	public RecoverWordViewModel(int index, string word, IReadOnlyCollection<string>? suggestions)
	{
		Index = index;
		_word = word;
		_suggestions = suggestions;
	}

	public int Index { get; }

	public override string ToString()
	{
		return $"{Index}. {Word}";
	}
}
