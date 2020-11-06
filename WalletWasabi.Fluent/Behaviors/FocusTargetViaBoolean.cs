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
		public static readonly StyledProperty<bool> FocusOnTargetProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(IsFocused), true);

		public bool IsFocused
		{
			get => GetValue(FocusOnTargetProperty);
			set => SetValue(FocusOnTargetProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.IsFocused)
				.Where(x => x)
				.Subscribe(_ => AssociatedObject?.Focus())
				.DisposeWith(disposables);
		}
	}
}