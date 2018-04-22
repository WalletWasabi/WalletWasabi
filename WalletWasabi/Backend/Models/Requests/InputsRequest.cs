using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WalletWasabi.Converters;

namespace WalletWasabi.Backend.Models.Requests
{
	public class InputsRequest
	{
		[Required]
		public IEnumerable<InputProofModel> Inputs { get; set; }

		[Required]
		[JsonConverter(typeof(ByteArrayConverter))]
		public byte[] BlindedOutput { get; set; }

		[Required]
		public string ChangeOutputScript { get; set; }
	}
}
