using Avalonia.Media;
using ReactiveUI;
using System.Linq;


namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

public partial class RecoveryWordViewModel : ViewModelBase
{
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private bool _isEnabled;
	[AutoNotify] private string? _selectedWord;
	[AutoNotify] private SolidColorBrush _confirmationWordColor;
	[AutoNotify] private SolidColorBrush _borderBackground;
	[AutoNotify] private SolidColorBrush _toggleBackground;
	[AutoNotify] private bool _isSelectedWordConfirmedWord;
	[AutoNotify] private bool _isNextWord;

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
		SelectedWord = Word; 
		IsSelected = ShouldBeVisible();
		IsConfirmed = IsSelected;
		IsEnabled = true;
		IsSelectedWordConfirmedWord = false;
		IsNextWord = false;
		ConfirmationWordColor = new SolidColorBrush(Colors.White);
		ToggleBackground = new SolidColorBrush(Colors.Transparent);
		BorderBackground = new SolidColorBrush(Color.Parse("#FAFAFA"));
	}

	private void ValidateWord()
	{
		IsConfirmed = Word.Equals(SelectedWord, StringComparison.InvariantCultureIgnoreCase);
	}

	public override string ToString()
	{
		return $"{Index}. {Word}";
	}

	internal bool ShouldBeVisible()
	{
		Random random = new Random();
		int[] excludedIndices = new int[3];
		excludedIndices[0] = 1;
		for (int i = 1; i < excludedIndices.Length; i++)
		{
			excludedIndices[i] = random.Next(2, 13);
		}
		return !excludedIndices.Contains(Index);
	}
}
