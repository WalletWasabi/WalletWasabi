using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class Setting<TOwner, TProperty> : ReactiveObject, IDisposable where TOwner : class, INotifyPropertyChanged
{
	private readonly CompositeDisposable _disposable = new();
	private TProperty _value = default!;

	public Setting(TOwner owner, Expression<Func<TOwner, TProperty?>> propertySelector)
	{
		owner.WhenAnyValue(propertySelector).BindTo(this, value => value.Value).DisposeWith(_disposable);
		this.WhenAnyValue(x => x.Value).Skip(1).BindTo(owner, propertySelector).DisposeWith(_disposable);
	}

	public TProperty Value
	{
		get => _value;
		set => this.RaiseAndSetIfChanged(ref _value, value);
	}

	public void Dispose() => _disposable.Dispose();
}
