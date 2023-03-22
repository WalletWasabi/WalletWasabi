using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

public partial class RecoveryWordViewModel : ViewModelBase
{
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private bool _isEnabled;
	[AutoNotify] private string? _selectedWord;

	public RecoveryWordViewModel(int index, string word)
	{
		Index = index;
		Word = word;

		this.WhenAnyValue(x => x.SelectedWord)
			.Subscribe(_ => ValidateWord());
	}

	public int Index { get; }
	public string Word { get; }

	public void Reset()
	{
		SelectedWord = null;
		IsSelected = false;
		IsConfirmed = false;
		IsEnabled = true;
	}

	private void ValidateWord()
	{
		IsConfirmed = Word.Equals(SelectedWord, StringComparison.InvariantCultureIgnoreCase);
	}

	public override string ToString()
	{
		return $"{Index}. {Word}";
	}
}
