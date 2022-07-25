using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;
public partial class AddressEntryControl : UserControl
{
	public AddressEntryControl()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public static readonly StyledProperty<string> WatermarkProperty = AvaloniaProperty.Register<AddressEntryControl, string>("Watermark");

	public string Watermark
	{
		get => GetValue(WatermarkProperty);
		set => SetValue(WatermarkProperty, value);
	}

	public static readonly StyledProperty<object> RightContentProperty = AvaloniaProperty.Register<AddressEntryControl, object>("RightContent");

	public object RightContent
	{
		get => GetValue(RightContentProperty);
		set => SetValue(RightContentProperty, value);
	}
}
