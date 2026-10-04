namespace WalletWasabi.Android;

internal static class AppIdentity
{
	public static int SocksPort => IsPersonal ? 37156 : 37154;
	public static int ControlPort => SocksPort + 1;
#if WASABI_PERSONAL
	public const string PackageName = "io.wasabiwallet.android.personal";
	public const string ApplicationLabel = "Wasabi Wallet";
	public static bool IsPersonal => true;
#else
	public const string PackageName = "io.wasabiwallet.android.dev";
	public const string ApplicationLabel = "Wasabi Wallet Test";
	public static bool IsPersonal => false;
#endif
}
