using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin
{
	public class InputsRequest
	{
		[Required]
		public long RoundId { get; set; }

		[Required, MinLength(1)]
		public IEnumerable<InputProofModel> Inputs { get; set; }

		[Required, MinLength(1)]
		[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter))]
		public IEnumerable<uint256> BlindedOutputScripts { get; set; }

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
