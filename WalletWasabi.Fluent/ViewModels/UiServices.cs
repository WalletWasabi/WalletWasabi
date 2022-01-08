namespace WalletWasabi.Fluent.ViewModels;

public static class UiServices
{
	public static WalletManagerViewModel WalletManager { get; private set; } = null!;

	public static void Initialize()
	{
		WalletManager = new WalletManagerViewModel();
	}
}
