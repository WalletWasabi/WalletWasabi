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

	public static JsonNode Filter(FilterModel filter) =>
		String(filter.ToLine());

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
			IEnumerable<string> s => Array(s.Select(String)),
			IEnumerable<uint256> u => Array(u.Select(UInt256)),
			string errorMessage => String(errorMessage),
			_ => throw new Exception($"{obj.GetType().FullName} is not known")
		};
}

public static partial class Decode
{
	public static readonly Decoder<FilterModel> Filter =
		String.Map(FilterModel.FromLine);

	public static readonly Decoder<FiltersResponse> FiltersResponse =
		Object(get => new FiltersResponse(
			get.Required("bestHeight", Int),
			get.Required("filters", Array(Filter))
		));
}
