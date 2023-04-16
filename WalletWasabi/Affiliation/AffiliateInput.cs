using NBitcoin;

namespace WalletWasabi.Affiliation;

public record AffiliateInput(OutPoint Prevout, Script ScriptPubKey, Money Amount, string AffiliationId, bool IsNoFee);
