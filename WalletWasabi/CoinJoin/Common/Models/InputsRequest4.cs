using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class InputsRequest4 : InputsRequestBase
	{
		[Required, MinLength(1)]
		public IEnumerable<BlindedOutputWithNonceIndex> BlindedOutputScripts { get; set; }
	}
}
