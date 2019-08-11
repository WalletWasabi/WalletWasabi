using Avalonia.Input;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	public class CommandOnFirstVisible : CommandBasedBehavior<InputElement>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.AttachedToVisualTree))
			.Take(1) // Only on first appearance.
			.Subscribe(_ =>
			{
				ExecuteCommand();
			}).DisposeWith(Disposables);
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
