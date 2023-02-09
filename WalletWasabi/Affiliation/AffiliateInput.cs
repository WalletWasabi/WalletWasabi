using NBitcoin;

namespace WalletWasabi.Affiliation;

public record AffiliateInput(OutPoint Prevout, Script ScriptPubKey, AffiliationFlag AffiliationFlag, bool IsNoFee);
