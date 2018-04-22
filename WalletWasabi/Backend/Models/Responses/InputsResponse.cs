using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
    public class InputsResponse
	{
		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] BlindedOutputSignature { get; set; }

		[JsonConverter(typeof(GuidJsonConverter))]
		public Guid UniqueId { get; set; }
	}
}
