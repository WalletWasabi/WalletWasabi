using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.CoinJoin
{
	public class InputProofModel
	{
		[Required]
		public TxoRef Input { get; set; }

		[Required]
		[MinLength(65, ErrorMessage = "Provided proof is invalid")] // Bitcoin compact signatures are 65 bytes length
		[MaxLength(65, ErrorMessage = "Provided proof is invalid")] // Bitcoin compact signatures are 65 bytes length
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] Proof { get; set; }
	}
}
