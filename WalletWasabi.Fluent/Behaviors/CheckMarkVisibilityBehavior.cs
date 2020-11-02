using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	public class CheckMarkVisibilityBehavior : AttachedToVisualTreeBehavior<PathIcon>
	{
		public static readonly StyledProperty<bool> HasErrorsProperty =
			AvaloniaProperty.Register<CheckMarkVisibilityBehavior, bool>(nameof(HasErrors), false);

		public static readonly StyledProperty<bool> IsFocusedProperty =
			AvaloniaProperty.Register<CheckMarkVisibilityBehavior, bool>(nameof(IsFocused), false);

		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<CheckMarkVisibilityBehavior, string>(nameof(Text), "");

		public bool HasErrors
		{
			get => GetValue(HasErrorsProperty);
			set => SetValue(HasErrorsProperty, value);
		}

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public static readonly StyledProperty<TextBox> OwnerTextBoxProperty =
			AvaloniaProperty.Register<CheckMarkVisibilityBehavior, TextBox>(nameof(OwnerTextBox), null!);

		public TextBox OwnerTextBox
		{
			get => GetValue(OwnerTextBoxProperty);
			set => SetValue(OwnerTextBoxProperty, value);
		}

		protected override void OnAttachedToVisualTree()
		{
			this.WhenAnyValue(
				x => x.HasErrors,
				x => x.IsFocused,
				x => x.Text)
				.Throttle(TimeSpan.FromMilliseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => AssociatedObject!.Opacity = !HasErrors && !IsFocused && !string.IsNullOrEmpty(Text) ? 1 : 0);

			Observable
				.FromEventPattern<AvaloniaPropertyChangedEventArgs>(OwnerTextBox, nameof(OwnerTextBox.PropertyChanged))
				.Select(x => x.EventArgs)
				.Where(x => x.Property.Name == "HasErrors")
				.Subscribe(x => HasErrors = (bool)x.NewValue!);
		}
	}
}