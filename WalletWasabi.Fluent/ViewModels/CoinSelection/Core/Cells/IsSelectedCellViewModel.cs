using System.Reactive.Disposables;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public partial class IsSelectedCellViewModel : ViewModelBase, ISelectable, IDisposable
{
	[AutoNotify] private bool _isSelected;
	private readonly CompositeDisposable _disposable = new();

	public IsSelectedCellViewModel(ISelectable selectable)
	{
		selectable.WhenAnyValue(h => h.IsSelected).Subscribe(b => IsSelected = b)
			.DisposeWith(_disposable);
		this.WhenAnyValue<IsSelectedCellViewModel, bool>(model => model.IsSelected).Subscribe(b => selectable.IsSelected = b)
			.DisposeWith(_disposable);
	}

	public void Dispose()
	{
		_disposable.Dispose();
	}
}
