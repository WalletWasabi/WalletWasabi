using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace HiddenWallet.GUI
{
	public class WalletClient
	{
		private HttpClient _httpClient = new HttpClient();
		private string baseUri = "http://localhost:49517/api/v1/wallet";

		public bool Exists()
		{
			HttpResponseMessage result = _httpClient.GetAsync(baseUri+"/wallet-exists/").Result;
			var json = JObject.Parse(result.Content.ReadAsStringAsync().Result);

			return json.Value<bool>("value");
		}
		public JObject Load(string password)
		{
			var json = new JObject
			{
				{ "password", password }
			};
			var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
			return JObject.Parse(_httpClient.PostAsync(baseUri+"/load", content).Result.Content.ReadAsStringAsync().Result);
		}
		public JObject Recover(string password, Mnemonic mnemonic, string creationTime)
		{
			var json = new JObject
			{
				{ "password", password },
				{ "mnemonic", mnemonic.ToString() },
				{ "creationTime", creationTime }
			};
			var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
			return JObject.Parse(_httpClient.PostAsync(baseUri+"/recover", content).Result.Content.ReadAsStringAsync().Result);
		}
		public JObject Create(string password)
		{
			var json = new JObject
			{
				{ "password", password }
			};
			var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
			return JObject.Parse(_httpClient.PostAsync(baseUri+"/create", content).Result.Content.ReadAsStringAsync().Result);
		}
	}
}
