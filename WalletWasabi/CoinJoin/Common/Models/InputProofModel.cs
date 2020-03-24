using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class InputProofModel
	{
		[Required]
		[JsonConverter(typeof(OutPointAsTxoRefJsonConverter))]
		public OutPoint Input { get; set; }

		[Required]
		[MinLength(65, ErrorMessage = "Provided proof is invalid")] // Bitcoin compact signatures are 65 bytes length
		[MaxLength(65, ErrorMessage = "Provided proof is invalid")] // Bitcoin compact signatures are 65 bytes length
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] Proof { get; set; }
	}
}
