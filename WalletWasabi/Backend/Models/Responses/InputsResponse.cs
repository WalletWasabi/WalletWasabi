using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class InputsResponse
	{
		public IEnumerable<string> BlindedOutputSignatures { get; set; }

		[JsonConverter(typeof(GuidJsonConverter))]
		public Guid UniqueId { get; set; }

		public long RoundId { get; set; }
	}
}
