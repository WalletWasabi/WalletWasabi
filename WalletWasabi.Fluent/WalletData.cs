using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public record WalletData(
	WalletId Id,
	string Name,
	bool IsHardwareWallet,
	bool IsWatchOnly,
	HDFingerprint? MasterFingerprint,
	Network Network,
	IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes
);
