using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WalletWasabi.Converters;

namespace WalletWasabi.Backend.Models
{
    public class InputProofModel
    {
		[Required]
		[JsonConverter(typeof(OutPointConverter))]
		public OutPoint	Input { get; set; }

		[Required]
		[JsonConverter(typeof(ByteArrayConverter))]
		public byte[] Proof { get; set; }
	}
}
