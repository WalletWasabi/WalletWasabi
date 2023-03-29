using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

/// <summary>
/// This class is the target of source-generated extension methods that enable fluent-style navigation APIs.
/// </summary>
public class FluentNavigate
{
	public FluentNavigate(UiContext uiContext)
	{
		UIContext = uiContext;
	}

	public UiContext UIContext { get; }
}
