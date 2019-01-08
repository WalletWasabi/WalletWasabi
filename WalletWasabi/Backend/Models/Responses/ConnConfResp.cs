using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend.Models.Responses
{
	public class ConnConfResp
	{
		[JsonProperty(ItemConverterType = typeof(BigIntegerJsonConverter))]
		public IEnumerable<BigInteger> BlindedOutputSignatures { get; set; }

		[Required]
		public CcjRoundPhase CurrentPhase { get; set; }
	}
}
