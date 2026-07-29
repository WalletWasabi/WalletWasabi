using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Textual helpers for BIP 77 <c>pj=</c> endpoint URLs.
/// A BIP 77 endpoint is a directory URL whose fragment carries uppercase bech32 parameters
/// delimited by <c>+</c> (or legacy <c>-</c>): <c>RK1…</c> (receiver key), <c>OH1…</c>
/// (OHTTP keys), <c>EX1…</c> (expiry). The payjoin-ffi <c>PjParam</c> handle exposes none
/// of these over FFI, so version dispatch and session-dedup keys are derived textually here.
/// Semantic parsing and validation stay in payjoin-ffi.
/// </summary>
public static class Bip77UriParams
{
	/// <summary>A pj endpoint is BIP 77 when its fragment carries the receiver-key and OHTTP-keys params.</summary>
	public static bool IsBip77(string pjEndpoint) =>
		TryGetFragmentParam(pjEndpoint, "RK1", out _) && TryGetFragmentParam(pjEndpoint, "OH1", out _);

	/// <summary>The receiver's ephemeral public key (opaque bech32 string) — the session-dedup key.</summary>
	public static bool TryGetReceiverKey(string pjEndpoint, [NotNullWhen(true)] out string? receiverKey) =>
		TryGetFragmentParam(pjEndpoint, "RK1", out receiverKey);

	private static bool TryGetFragmentParam(string pjEndpoint, string prefix, [NotNullWhen(true)] out string? value)
	{
		value = null;

		int fragmentStart = pjEndpoint.IndexOf('#');
		if (fragmentStart < 0 || fragmentStart == pjEndpoint.Length - 1)
		{
			return false;
		}

		// '-' is outside the bech32 character set, so splitting on both delimiters is safe.
		foreach (string param in pjEndpoint[(fragmentStart + 1)..].Split('+', '-'))
		{
			if (param.StartsWith(prefix, StringComparison.Ordinal))
			{
				value = param;
				return true;
			}
		}

		return false;
	}
}
