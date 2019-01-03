using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class InputsResponse
	{
		[Required, MinLength(1)]
		[JsonProperty(ItemConverterType = typeof(BigIntegerJsonConverter))]
		public IEnumerable<BigInteger> BlindedOutputSignatures { get; set; }

		[JsonConverter(typeof(GuidJsonConverter))]
		public Guid UniqueId { get; set; }

		public long RoundId { get; set; }
	}
}
