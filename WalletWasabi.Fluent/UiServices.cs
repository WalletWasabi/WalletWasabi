using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

public static class UiServices
{
	public static WalletManagerViewModel WalletManager { get; private set; } = null!;

	public static void Initialize(UiContext uiContext)
	{
		WalletManager = new WalletManagerViewModel(uiContext);
	}
}
