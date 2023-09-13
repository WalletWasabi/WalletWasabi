using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.CoinJoin.Common.Models;

public class InputsRequest4 : InputsRequestBase
{
	[Required, MinLength(1)]
	public IEnumerable<BlindedOutputWithNonceIndex> BlindedOutputScripts { get; set; }
}
