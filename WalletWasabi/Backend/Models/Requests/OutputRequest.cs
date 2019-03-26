using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Requests
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

		/// <summary>
		/// If UnblindedCredential is provided, then this is what's going to be signed, and OutputAddress can be arbitrary.
		/// This is in practice just a throwaway BitcoinAddress.
		/// </summary>
		[JsonConverter(typeof(BitcoinAddressJsonConverter))]
		public BitcoinAddress UnblindedCredential { get; set; }

		public StringContent ToHttpStringContent()
		{
			string jsonString = JsonConvert.SerializeObject(this, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}
	}
}
