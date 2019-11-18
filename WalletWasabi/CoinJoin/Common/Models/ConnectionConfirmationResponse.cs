using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class ConnectionConfirmationResponse
	{
		[JsonProperty(typeof(Uint256JsonConverter))]
		public IEnumerable<uint256> BlindedOutputSignatures { get; set; }

		[Required]
		public RoundPhase CurrentPhase { get; set; }
	}
}
