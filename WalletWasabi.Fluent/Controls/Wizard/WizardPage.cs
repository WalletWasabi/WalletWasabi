using Avalonia.Markup.Xaml.Templates;

namespace WalletWasabi.Fluent.Controls.Wizard;

public class WizardPage
{
	public DataTemplate? Template { get; set; }
	public string? NextText { get; set; }
	public bool IsNextVisible => !string.IsNullOrEmpty(NextText);
}
