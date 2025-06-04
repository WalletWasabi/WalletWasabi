using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Indexer.Models.Responses;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode VersionsResponse(VersionsResponse version) =>
		Object([
			("clientVersion", String(version.ClientVersion)),
			("BackenMajordVersion", String(version.IndexerMajorVersion)),
			("LegalDocumentsVersion", String("3.0")),
			("ww2LegalDocumentsVersion", String("2.0")),
			("commitHash", String(version.CommitHash)),
		]);

	private static JsonNode ExchangeRate(ExchangeRate rate) =>
		Object([
			("ticker", String(rate.Ticker)),
			("rate", Decimal(rate.Rate))
		]);

	public static JsonNode Filter(FilterModel filter) =>
		String(filter.ToLine());

	public static JsonNode FeeEstimations(Dictionary<int, int> estimations) =>
		Dictionary(estimations.ToDictionary(x => x.Key.ToString(), x => Int(x.Value)));

	public static JsonNode AllFeeEstimate(AllFeeEstimate estimate) =>
		Object([
			("estimations", FeeEstimations(estimate.Estimations))
		]);

	public static JsonNode SynchronizeResponse(SynchronizeResponse sync) =>
		Object([
			("filtersResponseState", Int((int)sync.FiltersResponseState)),
			("filters", Array(sync.Filters.Select(Filter))),
			("bestHeight", Int(sync.BestHeight)),
			("ccjRoundStates", Array(System.Array.Empty<JsonNode>())), // ww1 backward compatible
			("allFeeEstimate", Optional(sync.AllFeeEstimate, AllFeeEstimate)),
			("exchangeRates", Array(sync.ExchangeRates.Select(ExchangeRate))),
			("unconfirmedCoinJoins", Array(System.Array.Empty<JsonNode>()))	// ww1 indexer compatible
		]);

	public static JsonNode FiltersResponse(FiltersResponse resp) =>
		Object([
			("bestHeight", Int(resp.BestHeight)),
			("filters", Array(resp.Filters.Select(Filter)))
		]);

	public static JsonNode IndexerMessage<T>(T obj) =>
		obj switch
		{
			VersionsResponse version => VersionsResponse(version),
			AllFeeEstimate estimations => AllFeeEstimate(estimations),
			ExchangeRate exchangeRate => ExchangeRate(exchangeRate),
			SynchronizeResponse syncResp => SynchronizeResponse(syncResp),
			FiltersResponse filtersResp => FiltersResponse(filtersResp),
			Dictionary<int, int> feeEstimations => FeeEstimations(feeEstimations),
			IEnumerable<string> s => Array(s.Select(String)),
			IEnumerable<uint256> u => Array(u.Select(UInt256)),
			IEnumerable<ExchangeRate> e => Array(e.Select(ExchangeRate)),
			string errorMessage => String(errorMessage),
			_ => throw new Exception($"{obj.GetType().FullName} is not known")
		};
}

public static partial class Decode
{
	public static readonly Decoder<AllFeeEstimate> AllFeeEstimate =
		Object(get => new AllFeeEstimate(
			get.Required("estimations", Dictionary(Int)).ToDictionary(x => int.Parse(x.Key), x => x.Value)
		));

	public static readonly Decoder<FilterModel> Filter =
		String.Map(FilterModel.FromLine);

	public static readonly Decoder<FiltersResponse> FiltersResponse =
		Object(get => new FiltersResponse(
			get.Required("bestHeight", Int),
			get.Required("filters", Array(Filter))
		));
}
