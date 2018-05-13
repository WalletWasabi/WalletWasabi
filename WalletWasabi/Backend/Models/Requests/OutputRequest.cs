using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;

namespace WalletWasabi.Backend.Models.Requests
{
    public class OutputRequest
    {
		[Required]
		public string OutputAddress { get; set; }

		[Required]
		public string SignatureHex { get; set; }

		public StringContent ToHttpStringContent()
		{
			string jsonString = JsonConvert.SerializeObject(this, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}
	}
}
