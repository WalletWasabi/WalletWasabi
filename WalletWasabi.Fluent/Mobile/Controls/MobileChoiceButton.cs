using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>A wrapping, keyboard-accessible navigation row with a native Button command.</summary>
public sealed class MobileChoiceButton : Button
{
	public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<MobileChoiceButton, string>(nameof(Label), "");
	public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<MobileChoiceButton, string>(nameof(Description), "");
	public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<MobileChoiceButton, string>(nameof(Icon), "leaf");
	public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
	public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
	public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == LabelProperty) AutomationProperties.SetName(this, Label);
	}
}
