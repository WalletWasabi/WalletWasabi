using NBitcoin;

namespace WalletWasabi.Affiliation;

public record AffiliateInput(OutPoint Prevout, Script ScriptPubKey, string AffiliationId, bool IsNoFee);
