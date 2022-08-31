using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public partial class IsSelectedThreeStateCellViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposable = new();
	[AutoNotify] private bool? _isSelected;

	public IsSelectedThreeStateCellViewModel(IThreeStateSelectable selectable)
	{
		selectable.WhenAnyValue(h => h.TreeStateSelection).Select(ConvertSelection.From)
			.Do(b => IsSelected = b)
			.Subscribe()
			.DisposeWith(_disposable);

		this.WhenAnyValue(model => model.IsSelected)
			.Select(ConvertSelection.To)
			.Do(b => selectable.TreeStateSelection = b)
			.Subscribe()
			.DisposeWith(_disposable);
	}

	public void Dispose()
	{
		_disposable.Dispose();
	}
}
