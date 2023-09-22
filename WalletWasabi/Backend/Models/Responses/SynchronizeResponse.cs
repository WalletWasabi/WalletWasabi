using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses;

public class SynchronizeResponse
{
	public FiltersResponseState FiltersResponseState { get; set; }

	[JsonProperty(ItemConverterType = typeof(FilterModelJsonConverter))] // Do not use the default jsonifyer, because that's too much data.
	public IEnumerable<FilterModel> Filters { get; set; } = new List<FilterModel>();

	public int BestHeight { get; set; }

	// Property was used in WW1. Leaving it here for backward compatibility.
	// Note: Each empty enumerable is serialized the same to JSON, so we can use any type here.
	public IEnumerable<int> CcjRoundStates { get; } = Array.Empty<int>();

	public AllFeeEstimate? AllFeeEstimate { get; set; }

	public IEnumerable<ExchangeRate> ExchangeRates { get; set; } = new List<ExchangeRate>();

	// Property was used in WW1. Leaving it here for backward compatibility.
	[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter))]
	public IEnumerable<uint256> UnconfirmedCoinJoins { get; } = Array.Empty<uint256>();
}
