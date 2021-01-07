using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public class QuestionControl : Label
	{
		public static readonly StyledProperty<ICommand> YesCommandProperty =
			AvaloniaProperty.Register<QuestionControl, ICommand>(nameof(YesCommand));

		public static readonly StyledProperty<ICommand> NoCommandProperty =
			AvaloniaProperty.Register<QuestionControl, ICommand>(nameof(NoCommand));

		public static readonly StyledProperty<IImage> IconProperty =
			AvaloniaProperty.Register<QuestionControl, IImage>(nameof(Icon));

		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<QuestionControl, string>(nameof(Text));

		public ICommand YesCommand
		{
			get => GetValue(YesCommandProperty);
			set => SetValue(YesCommandProperty, value);
		}

		public ICommand NoCommand
		{
			get => GetValue(NoCommandProperty);
			set => SetValue(NoCommandProperty, value);
		}

		public IImage Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}
	}


}