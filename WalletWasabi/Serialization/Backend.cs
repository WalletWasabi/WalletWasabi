using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	public static JsonNode VersionsResponse(VersionsResponse version) =>
		Object([
			("BackenMajordVersion", String(version.BackendMajorVersion)),
		]);

	private static JsonNode Filter(FilterModel filter) =>
		String(filter.ToLine());

	public static JsonNode FiltersResponse(FiltersResponse resp) =>
		Object([
			("bestHeight", UInt(resp.BestHeight)),
			("filters", Array(resp.Filters.Select(Filter)))
		]);
}

public static partial class Decode
{
	private static Decoder<FilterModel> Filter =>
		String.Map(FilterModel.FromLine);

	public static Decoder<FiltersResponse> FiltersResponse =>
		Object(get => new FiltersResponse(
			get.Required("bestHeight", UInt),
			get.Required("filters", Array(Filter))
		));
}
