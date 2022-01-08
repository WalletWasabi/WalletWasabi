using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models;

public class InputsResponse
{
	[JsonConverter(typeof(GuidJsonConverter))]
	public Guid UniqueId { get; set; }

	public long RoundId { get; set; }
}
