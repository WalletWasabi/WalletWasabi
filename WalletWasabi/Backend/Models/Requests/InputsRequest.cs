using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Requests
{
	public class InputsRequest
	{
		[Required]
		public IEnumerable<InputProofModel> Inputs { get; set; }

		[Required]
		public IEnumerable<string> BlindedOutputScripts { get; set; }

		[Required]
		[JsonConverter(typeof(BitcoinAddressJsonConverter))]
		public BitcoinAddress ChangeOutputAddress { get; set; }

		public StringContent ToHttpStringContent()
		{
			string jsonString = JsonConvert.SerializeObject(this, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}
	}
}
