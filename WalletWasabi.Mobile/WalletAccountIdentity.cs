using NBitcoin;
using System.Security.Cryptography;
using System.Text;

namespace WalletWasabi.Mobile;

internal static class WalletAccountIdentity
{
    // Use the same canonical public-key encoding as encrypted Wasabi backups.
    // ExtPubKey inherits Object.ToString(), which cannot identify an account.
    public static string Reference(Network network, KeyPath account, ExtPubKey key) =>
        Hash(network.Name + ":" + account + ":" + key.GetWif(Network.Main).ToWif());

    public static string LegacyReference(Network network, KeyPath account) =>
        Hash(network.Name + ":" + account + ":NBitcoin.ExtPubKey");

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal sealed record Ownership(string Reference, string LegacyReference, IReadOnlySet<OutPoint> Inputs);

    public static string? ResolveLegacyOwner(string legacyReference, IEnumerable<OutPoint> inputs, IEnumerable<Ownership> accounts)
    {
        var reserved = inputs.ToHashSet();
        if (reserved.Count == 0) { return null; }
        var matches = accounts.Where(a => a.LegacyReference == legacyReference && reserved.All(a.Inputs.Contains))
            .Select(a => a.Reference).Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
