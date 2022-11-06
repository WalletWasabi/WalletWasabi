using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.WabiSabi.Models;

public record ReadyToSignRequestRequest(uint256 RoundId, Guid AliceId, [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate), DefaultAffiliationFlag()] AffiliationFlag AffiliationFlag);
