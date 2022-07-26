using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls.Mixins;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class AmountCurrencyDirectionController : ReactiveObject, IDisposable
{
	private bool _isReversed;
	private readonly CompositeDisposable _disposables = new();

	public AmountCurrencyDirectionController(bool initialValue, Action<bool> setIsReversed)
	{
		IsReversed = initialValue;
		DisposableMixin.DisposeWith(
				this.WhenAnyValue(x => x.IsReversed)
					.Do(setIsReversed)
					.Subscribe(), _disposables);
	}

	public bool IsReversed
	{
		get => _isReversed;
		set => this.RaiseAndSetIfChanged(ref _isReversed, value);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
