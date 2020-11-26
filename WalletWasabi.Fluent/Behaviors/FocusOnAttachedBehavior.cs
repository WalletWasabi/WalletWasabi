using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusOnAttachedBehavior : AttachedToVisualTreeBehavior<Control>
	{
		public static readonly StyledProperty<bool> IsEnabledProperty =
			AvaloniaProperty.Register<FocusOnAttachedBehavior, bool>(nameof(IsEnabled), true);

		public bool IsEnabled
		{
			get => GetValue(IsEnabledProperty);
			set => SetValue(IsEnabledProperty, value);
		}

		protected override void OnAttachedToVisualTree()
		{
			if (IsEnabled)
			{
				AssociatedObject?.Focus();
			}
		}
	}
}