using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode VersionsResponse(VersionsResponse version) =>
		Object([
			("clientVersion", String(version.ClientVersion)),
			("BackenMajordVersion", String(version.BackendMajorVersion)),
			("LegalDocumentsVersion", String("3.0")),
			("ww2LegalDocumentsVersion", String("2.0")),
			("commitHash", String(version.CommitHash)),
		]);

	public static JsonNode Filter(FilterModel filter) =>
		String(filter.ToLine());

	public static JsonNode FeeEstimations(Dictionary<int, FeeRate> estimations) =>
		Dictionary(estimations.ToDictionary(x => x.Key.ToString(), x => FeeRate(x.Value)));

	public static JsonNode FiltersResponse(FiltersResponse resp) =>
		Object([
			("bestHeight", Int(resp.BestHeight)),
			("filters", Array(resp.Filters.Select(Filter)))
		]);

	public static JsonNode BackendMessage<T>(T obj) =>
		obj switch
		{
			VersionsResponse version => VersionsResponse(version),
			FiltersResponse filtersResp => FiltersResponse(filtersResp),
			Dictionary<int, FeeRate> feeEstimations => FeeEstimations(feeEstimations),
			IEnumerable<string> s => Array(s.Select(String)),
			IEnumerable<uint256> u => Array(u.Select(UInt256)),
			string errorMessage => String(errorMessage),
			_ => throw new Exception($"{obj.GetType().FullName} is not known")
		};
}

public static partial class Decode
{
	public static readonly Decoder<AllFeeEstimate> AllFeeEstimate =
		Object(get => new AllFeeEstimate(
			get.Required("estimations", Dictionary(FeeRate)).ToDictionary(x => int.Parse(x.Key), x => x.Value)
		));

	public static readonly Decoder<FilterModel> Filter =
		String.Map(FilterModel.FromLine);

	public static readonly Decoder<FiltersResponse> FiltersResponse =
		Object(get => new FiltersResponse(
			get.Required("bestHeight", Int),
			get.Required("filters", Array(Filter))
		));
}
