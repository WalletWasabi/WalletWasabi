using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ExecuteCommandOnClickBehavior : AttachedToVisualTreeBehavior<Control>
	{
		public static readonly StyledProperty<ICommand?> CommandProperty =
			AvaloniaProperty.Register<ExecuteCommandOnClickBehavior, ICommand?>(nameof(Command));

		public ICommand? Command
		{
			get => GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		}

		protected override void OnAttachedToVisualTree()
		{
			AssociatedObject.PointerReleased += OnPointerReleased;
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			AssociatedObject.PointerReleased -= OnPointerReleased;
		}

		private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			Command?.Execute(default);
		}
	}
}
