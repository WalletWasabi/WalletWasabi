using NBitcoin.BouncyCastle.Math;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin
{
	public class InputsResponse
	{
		[JsonConverter(typeof(GuidJsonConverter))]
		public Guid UniqueId { get; set; }

		public long RoundId { get; set; }
	}
}
