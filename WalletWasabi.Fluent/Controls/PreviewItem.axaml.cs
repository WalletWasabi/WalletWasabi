using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public class PreviewItem : ContentControl
	{
		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<PreviewItem, string>(nameof(Text));

		public static readonly StyledProperty<Geometry> IconProperty =
			AvaloniaProperty.Register<PreviewItem, Geometry>(nameof(Icon));

		public static readonly StyledProperty<double> IconSizeProperty =
			AvaloniaProperty.Register<PreviewItem, double>(nameof(IconSize), 24);

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public Geometry Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		public double IconSize
		{
			get => GetValue(IconSizeProperty);
			set => SetValue(IconSizeProperty, value);
		}
	}
}
