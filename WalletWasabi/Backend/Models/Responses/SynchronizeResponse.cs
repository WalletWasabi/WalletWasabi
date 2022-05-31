using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses;

public class SynchronizeResponse
{
	public FiltersResponseState FiltersResponseState { get; set; }

	[JsonProperty(ItemConverterType = typeof(FilterModelJsonConverter))] // Do not use the default jsonifyer, because that's too much data.
	public IEnumerable<FilterModel> Filters { get; set; } = new List<FilterModel>();

	public int BestHeight { get; set; }

	public IEnumerable<RoundStateResponseBase> CcjRoundStates { get; set; } = new List<RoundStateResponseBase>();

	public AllFeeEstimate? AllFeeEstimate { get; set; }

	public IEnumerable<ExchangeRate> ExchangeRates { get; set; } = new List<ExchangeRate>();

	[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter))]
	public IEnumerable<uint256> UnconfirmedCoinJoins { get; set; } = new List<uint256>();
}
