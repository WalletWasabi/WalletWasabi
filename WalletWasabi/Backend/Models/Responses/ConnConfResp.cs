using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend.Models.Responses
{
    public class ConnConfResp
	{
		[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter))]
		public IEnumerable<uint256> BlindedOutputSignatures { get; set; }

		[Required]
		public CcjRoundPhase CurrentPhase { get; set; }
	}
}
