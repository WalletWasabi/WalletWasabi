using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class OutputRequest
	{
		[Required]
		[JsonConverter(typeof(BitcoinAddressJsonConverter))]
		public BitcoinAddress OutputAddress { get; set; }

		[Required]
		public int Level { get; set; }

		[Required]
		[JsonConverter(typeof(UnblindedSignatureJsonConverter))]
		public UnblindedSignature UnblindedSignature { get; set; }

		public StringContent ToHttpStringContent()
		{
			string jsonString = JsonConvert.SerializeObject(this, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}
	}
}
