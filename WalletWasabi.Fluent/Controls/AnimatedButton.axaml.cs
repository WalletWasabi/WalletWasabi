using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public class AnimatedButton : TemplatedControl
	{
		public static readonly StyledProperty<ICommand> CommandProperty =
			AvaloniaProperty.Register<AnimatedButton, ICommand>(nameof(Command));

		public static readonly StyledProperty<Geometry> NormalIconProperty =
			AvaloniaProperty.Register<AnimatedButton, Geometry>(nameof(NormalIcon));

		public static readonly StyledProperty<Geometry> ClickIconProperty =
			AvaloniaProperty.Register<AnimatedButton, Geometry>(nameof(ClickIcon));

		public ICommand Command
		{
			get => GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		}

		public Geometry NormalIcon
		{
			get => GetValue(NormalIconProperty);
			set => SetValue(NormalIconProperty, value);
		}

		public Geometry ClickIcon
		{
			get => GetValue(ClickIconProperty);
			set => SetValue(ClickIconProperty, value);
		}
	}
}