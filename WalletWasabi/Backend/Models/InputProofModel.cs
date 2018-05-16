using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models
{
	public class InputProofModel
	{
		[Required]
		[JsonConverter(typeof(OutPointJsonConverter))]
		public OutPoint Input { get; set; }

		[Required]
		public string Proof { get; set; }
	}
}
