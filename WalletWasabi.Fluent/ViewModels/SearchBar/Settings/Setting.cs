using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class Setting<TOwner, TProperty> : ReactiveObject, IDisposable where TOwner : class, INotifyPropertyChanged
{
	private TProperty _value;
	private readonly IDisposable _subscription;

	public Setting(TOwner owner, Expression<Func<TOwner, TProperty?>> propertySelector)
	{
		_subscription = owner.WhenAnyValue(propertySelector).BindTo(this, value => value.Value);
		_subscription = this.WhenAnyValue(x => x.Value).Skip(1).BindTo(owner, propertySelector);
	}

	public TProperty Value
	{
		get => _value;
		set => this.RaiseAndSetIfChanged(ref _value, value);
	}

	public void Dispose() => _subscription.Dispose();
}
