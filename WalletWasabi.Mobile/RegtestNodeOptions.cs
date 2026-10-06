using NBitcoin;
using NBitcoin.RPC;

namespace WalletWasabi.Mobile;

/// <summary>Explicit Bitcoin Core injection for synthetic regtest qualification, never public-network settings.</summary>
public sealed record RegtestNodeOptions(string Uri, string Credentials)
{
    internal void Validate(Network network)
    {
        if (network != Network.RegTest) { throw new InvalidOperationException("A qualification node can only serve regtest."); }
        if (Uri is null || Credentials is null || !System.Uri.TryCreate(Uri, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback
            || endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length > 0 || endpoint.Fragment.Length > 0
            || !RPCCredentialString.TryParse(Credentials, out _))
        { throw new FormatException("Use explicit localhost regtest qualification credentials."); }
    }
}
