using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusNextTargetViaBoolean : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> FocusOnTargetProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(FocusOnTarget), true);

		public bool FocusOnTarget
		{
			get => GetValue(FocusOnTargetProperty);
			set => SetValue(FocusOnTargetProperty, value);
		}

		public static readonly StyledProperty<Control> TargetControlProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, Control>(nameof(TargetControl));

		[ResolveByName]
		public Control TargetControl
		{
			get => GetValue(TargetControlProperty);
			set => SetValue(TargetControlProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.FocusOnTarget)
				.Where(x => x)
				.Subscribe(_ => TargetControl?.Focus())
				.DisposeWith(disposables);
		}
	}
}