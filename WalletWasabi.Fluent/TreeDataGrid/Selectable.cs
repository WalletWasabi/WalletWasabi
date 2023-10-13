using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.TreeDataGrid;

public partial class Selectable<T> : ReactiveObject
{
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _canSelect;

	public Selectable(T model, Action<T>? onSelected = null, IObservable<bool>? canSelect = null)
	{
		canSelect ??= Observable.Return(true);

		Model = model;
		ToggleSelectionCommand = ReactiveCommand.Create(() => IsSelected = !IsSelected, canSelect);

		if (onSelected is { } action)
		{
			this.WhenAnyValue(x => x.IsSelected)
				.CombineLatest(canSelect)
				.Where(x => x.Second)
				.Do(_ => action(Model))
				.Subscribe();
		}

		canSelect.BindTo(this, x => x.CanSelect);
	}

	public Selectable(T model, Func<T, Task> onSelectedAsync, IObservable<bool>? canSelect = null)
	{
		canSelect ??= Observable.Return(true);

		Model = model;
		ToggleSelectionCommand = ReactiveCommand.Create(() => IsSelected = !IsSelected, canSelect);

		this.WhenAnyValue(x => x.IsSelected)
			.CombineLatest(canSelect)
			.Where(x => x.Second)
			.DoAsync(async _ => await onSelectedAsync(Model))
			.Subscribe();

		canSelect.BindTo(this, x => x.CanSelect);
	}

	public ICommand ToggleSelectionCommand { get; }

	public T Model { get; }
}
