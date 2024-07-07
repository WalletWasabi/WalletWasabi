using System.Linq;
using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Models;

public record ReadyToSignRequestRequest
{
	[JsonConstructor]
	public ReadyToSignRequestRequest(
		uint256 roundId,
		Guid aliceId)
	{
		RoundId = roundId;
		AliceId = aliceId;
	}
	public uint256 RoundId { get; }
	public Guid AliceId { get; }
}
