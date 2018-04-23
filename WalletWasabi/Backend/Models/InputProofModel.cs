using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models
{
    public class InputProofModel
    {
		[Required]
		[JsonConverter(typeof(OutPointJsonConverter))]
		public OutPoint	Input { get; set; }

		[Required]
		public string Proof { get; set; }
	}
}
