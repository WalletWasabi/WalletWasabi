using System.Collections.Immutable;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Models
{
	public record RoundStateRequest(ImmutableList<(uint256 RoundId, long EventId)> RoundCheckpoints)
	{
		public static readonly RoundStateRequest Empty = new (ImmutableList<(uint256 RoundId, long EventId)>.Empty);
	}
}
