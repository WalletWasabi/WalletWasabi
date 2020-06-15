using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class InputsRequest : InputsRequestBase
	{
		[Required, MinLength(1)]
		[JsonProperty(ItemConverterType = typeof(Uint256JsonConverter))]
		public IEnumerable<uint256> BlindedOutputScripts { get; set; }
	}
}
