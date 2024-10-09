using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BitcoinCore.Rpc;

public static class RpcErrorTools
{
	public const string SpentError1 = "bad-txns-inputs-missingorspent";
	public const string SpentError2 = "missing-inputs";
	public const string SpentError3 = "txn-mempool-conflict";
	public const string TooLongMempoolChainError = "too-long-mempool-chain";

	public const string SpentErrorTranslation = "At least one coin you are trying to spend is already spent.";

	public static Dictionary<string, string> ErrorTranslations { get; } = new Dictionary<string, string>
	{
		[TooLongMempoolChainError] = "At least one coin you are trying to spend is part of long or heavy chain of unconfirmed transactions. You must wait for some previous transactions to confirm.",
		[SpentError1] = SpentErrorTranslation,
		[SpentError2] = SpentErrorTranslation,
		[SpentError3] = SpentErrorTranslation,
		["bad-txns-inputs-duplicate"] = "The transaction contains duplicated inputs.",
		["bad-txns-nonfinal"] = "The transaction is not final and cannot be broadcast.",
		["bad-txns-oversize"] = "The transaction is too big.",

		["invalid password"] = "Wrong passphrase.",
		["Invalid wallet name"] = "Invalid wallet name.",
		["Wallet name is already taken"] = "Wallet name is already taken."
	};

	public static bool IsSpentError(string error)
	{
		return new[] { SpentError1, SpentError2, SpentError3 }.Any(x => error.Contains(x, StringComparison.OrdinalIgnoreCase));
	}

	public static bool IsTooLongMempoolChainError(string error)
		=> error.Contains(TooLongMempoolChainError, StringComparison.OrdinalIgnoreCase);
}
