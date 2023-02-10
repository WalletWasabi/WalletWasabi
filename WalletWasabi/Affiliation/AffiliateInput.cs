using NBitcoin;

	
namespace WalletWasabi.Affiliation;

public record AffiliateInput(OutPoint Prevout, Script ScriptPubKey, string AffiliationFlag, bool IsNoFee);
