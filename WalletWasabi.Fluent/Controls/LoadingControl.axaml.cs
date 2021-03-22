using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls
{
	public class LoadingControl : UserControl
	{
		public static readonly StyledProperty<double> PercentProperty =
			AvaloniaProperty.Register<LoadingControl, double>(nameof(Percent));

		public static readonly StyledProperty<string> StatusTextProperty =
			AvaloniaProperty.Register<LoadingControl, string>(nameof(StatusText));

		public double Percent
		{
			get => GetValue(PercentProperty);
			set => SetValue(PercentProperty, value);
		}

		public string StatusText
		{
			get => GetValue(StatusTextProperty);
			set => SetValue(StatusTextProperty, value);
		}
	}
}
