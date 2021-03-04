using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class InputOwnershipProofModel
	{
		[Required]
		[JsonConverter(typeof(OutPointAsTxoRefJsonConverter))]
		public OutPoint Input { get; set; }

		// Minimum length of P2WPHK proof of ownership is (4 + 1 + 1 + 32) + (1 + 1 + 1 + (1 + 1 + 1 + 1 + 1 + 1 + 1 + 1 + 1) + 1 + 33) = 84
		// Typical length of P2WPHK proof of ownership is (4 + 1 + 1 + 32) + (1 + 1 + 1 + (1 + 1 + 1 + 1 + 32 + 1 + 1 + 32 + 1) + 1 + 33) = 147
		// Maximum length of P2WPHK proof of ownership is (4 + 1 + 1 + 32) + (8 + 8 + 8 + (1 + 1 + 1 + 1 + 33 + 1 + 1 + 33 + 1) + 8 + 33) = 176
		[Required]
		[MinLength(84, ErrorMessage = "Provided proof is invalid")]
		[MaxLength(176, ErrorMessage = "Provided proof is invalid")]
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] Proof { get; set; }
	}
}
