using Newtonsoft.Json;
using System;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class InputsResponse
	{
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] BlindedOutputSignature { get; set; }

		[JsonConverter(typeof(GuidJsonConverter))]
		public Guid UniqueId { get; set; }

		public long RoundId { get; set; }
	}
}
