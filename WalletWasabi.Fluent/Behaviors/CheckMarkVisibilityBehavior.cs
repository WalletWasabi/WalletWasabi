using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Behaviors
{
	public class CheckMarkVisibilityBehavior : DisposingBehavior<PathIcon>
	{
		public static readonly StyledProperty<TextBox> OwnerTextBoxProperty =
			AvaloniaProperty.Register<CheckMarkVisibilityBehavior, TextBox>(nameof(OwnerTextBox), null!);

		[ResolveByName]
		public TextBox OwnerTextBox
		{
			get => GetValue(OwnerTextBoxProperty);
			set => SetValue(OwnerTextBoxProperty, value);
		}


		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(
				x => x.HasErrors,
				x => x.IsFocused,
				x => x.Text)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Where(_ => AssociatedObject is { })
				.Subscribe(_ => AssociatedObject!.Opacity = !HasErrors && !IsFocused && !string.IsNullOrEmpty(Text) ? 1 : 0);

			Observable
				.FromEventPattern<AvaloniaPropertyChangedEventArgs>(OwnerTextBox, nameof(OwnerTextBox.PropertyChanged))
				.Select(x => x.EventArgs)
				.Where(x => x.Property.Name == "HasErrors")
				.Subscribe(x => HasErrors = (bool)x.NewValue!);
		}
	}
}