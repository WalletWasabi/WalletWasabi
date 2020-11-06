using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusTargetViaBoolean : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> GotFocusedProperty =
			AvaloniaProperty.Register<FocusTargetViaBoolean, bool>(nameof(GotFocused));

		public bool GotFocused
		{
			get => GetValue(GotFocusedProperty);
			set => SetValue(GotFocusedProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.GotFocused)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject?.Focus())
				.DisposeWith(disposables);
		}
	}
}