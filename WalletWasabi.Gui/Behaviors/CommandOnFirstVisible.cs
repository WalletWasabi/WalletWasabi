using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Behaviors
{
	internal class CommandOnFirstVisible : CommandBasedBehavior<InputElement>
	{
		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.AttachedToVisualTree))
			.Take(1)
			.Subscribe(async args =>
			{
				await Task.Delay(1000);
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
