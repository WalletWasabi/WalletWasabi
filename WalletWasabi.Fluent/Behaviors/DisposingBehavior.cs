using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	public abstract class DisposingBehavior<T> : Behavior<T> where T : class, IAvaloniaObject
	{
		private CompositeDisposable? _disposables;

		protected override void OnAttached()
		{
			base.OnAttached();

			_disposables?.Dispose();

			_disposables = new CompositeDisposable();

			OnAttached(_disposables);
		}

		protected abstract void OnAttached(CompositeDisposable disposables);

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposables?.Dispose();
		}
	}
}