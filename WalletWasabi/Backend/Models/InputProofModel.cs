using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models
{
	public class InputProofModel
	{
		[Required]
		public TxoRef Input { get; set; }

		[Required]
		public string Proof { get; set; }
	}
}
