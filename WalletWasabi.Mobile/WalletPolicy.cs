using NBitcoin;

namespace WalletWasabi.Mobile;

public sealed record WalletPolicy
{
  private WalletPolicy(bool mainnetAllowed) => MainnetAllowed = mainnetAllowed;
  public bool MainnetAllowed { get; }
  public static WalletPolicy Development { get; } = new(false);
  public static WalletPolicy Personal { get; } = new(true);
  public void RequireNetwork(Network network)
  {
#if WASABI_DEVELOPMENT_ANDROID
    // Compiled into the development session assembly. A caller cannot bypass
    // the restriction by supplying a different policy object.
    if (network == Network.Main) { throw new InvalidOperationException("Development builds cannot operate mainnet wallets."); }
#endif
    if (!MainnetAllowed && network == Network.Main)
    {
      throw new InvalidOperationException("Development builds cannot operate mainnet wallets.");
    }
  }
}
