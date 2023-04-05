using NBitcoin;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation;

public record AffiliateInput(OutPoint Prevout, Script ScriptPubKey, Money Amount, [field: CanonicalJsonIgnore] string AffiliationId, bool IsNoFee);
