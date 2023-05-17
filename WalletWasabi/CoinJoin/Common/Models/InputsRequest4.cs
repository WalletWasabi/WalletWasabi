using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace WalletWasabi.CoinJoin.Common.Models;

public class InputsRequest4 : InputsRequestBase
{
	[Required, MinLength(1)]
	public IEnumerable<BlindedOutputWithNonceIndex> BlindedOutputScripts { get; set; }
}
