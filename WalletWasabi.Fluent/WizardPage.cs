using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;

namespace WalletWasabi.Fluent;

public class WizardPage
{
	[Content]
	public DataTemplate Template { get; set; }

	public string NextText { get; set; }
	public bool IsNextVisible => !string.IsNullOrEmpty(NextText);
}
