using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public partial class IsSelectedThreeStateViewModel : ViewModelBase, IDisposable
{
	[AutoNotify] private bool? _isSelected;
	private readonly CompositeDisposable _disposable = new();

	public IsSelectedThreeStateViewModel(IThreeState selectable)
	{
		selectable.WhenAnyValue(h => h.SelectionState).Select(ConvertSelection.From)
			.Do(b => IsSelected = b)
			.Subscribe()
			.DisposeWith(_disposable);

		this.WhenAnyValue(model => model.IsSelected)
			.Select(ConvertSelection.To)
			.Do(b => selectable.SelectionState = b)
			.Subscribe()
			.DisposeWith(_disposable);
	}

	public void Dispose()
	{
		_disposable.Dispose();
	}
}

public static class ConvertSelection
{
	public static bool? From(SelectionState state)
	{
		if (state == SelectionState.False)
		{
			return false;
		}

		if (state == SelectionState.True)
		{
			return true;
		}

		return null;
	}

	public static SelectionState To(bool? state)
	{
		if (state == true)
		{
			return SelectionState.True;
		}

		if (state == false)
		{
			return SelectionState.False;
		}

		return SelectionState.Partial;
	}
}
