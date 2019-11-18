using System.Collections.Generic;
using System.Text.Json.Serialization;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class SynchronizeResponse
	{
		public FiltersResponseState FiltersResponseState { get; set; }

		[JsonConverter(typeof(FilterModelJsonConverter))] // Do not use the deafult jsonifyer, because that's too much data.
		public IEnumerable<FilterModel> Filters { get; set; }

		public int BestHeight { get; set; }

		public IEnumerable<RoundStateResponse> CcjRoundStates { get; set; }

		public AllFeeEstimate AllFeeEstimate { get; set; }

		public IEnumerable<ExchangeRate> ExchangeRates { get; set; }
	}
}
