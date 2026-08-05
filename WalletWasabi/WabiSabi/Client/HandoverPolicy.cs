using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// Decides whether a coinjoin round's outputs stay in the source wallet or are handed over to the
/// wallet the user selected as the coinjoin destination.
/// </summary>
/// <remarks>
/// A wallet mixing into a different wallet keeps its outputs at home until it reaches its own
/// anonymity score target, and only then starts delivering them. Handing over sooner means a coin
/// leaves after a single round, far below the target, and the source wallet can no longer remix it
/// because it no longer owns it.
/// </remarks>
public static class HandoverPolicy
{
	/// <summary>Whether the wallet is configured to coinjoin into a different wallet.</summary>
	public static bool IsMixingToOtherWallet(WalletId source, WalletId destination) =>
		source != destination;

	/// <summary>Whether this round's outputs should be delivered to the destination wallet.</summary>
	public static bool IsReadyForHandover(WalletId source, WalletId destination, bool isSourceWalletPrivate) =>
		IsMixingToOtherWallet(source, destination) && isSourceWalletPrivate;

	/// <summary>
	/// Resolves the persisted destination name against the wallets currently loaded, falling back to
	/// the source wallet itself.
	/// </summary>
	/// <remarks>
	/// The destination may have been renamed, deleted, or simply not loaded yet, since wallets load in
	/// an arbitrary order. Mixing into self is the safe fallback and matches what users already get
	/// today, when the selection is lost on every restart.
	/// </remarks>
	public static string ResolveDestinationWalletName(
		string sourceWalletName,
		string? persistedDestinationName,
		IReadOnlyCollection<string> loadedWalletNames) =>
		persistedDestinationName is { } name && loadedWalletNames.Contains(name)
			? name
			: sourceWalletName;
}
