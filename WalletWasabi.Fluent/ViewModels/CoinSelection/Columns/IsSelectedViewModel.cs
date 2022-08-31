using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Model;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public partial class IsSelectedViewModel : ViewModelBase, ISelectable, IDisposable
{
	[AutoNotify] private bool _isSelected;
	private readonly CompositeDisposable _disposable = new();

	public IsSelectedViewModel(ISelectable selectable)
	{
		selectable.WhenAnyValue(h => h.IsSelected).Subscribe(b => IsSelected = b)
			.DisposeWith(_disposable);
		this.WhenAnyValue(model => model.IsSelected).Subscribe(b => selectable.IsSelected = b)
			.DisposeWith(_disposable);
	}

	public void Dispose()
	{
		_disposable.Dispose();
	}
}
