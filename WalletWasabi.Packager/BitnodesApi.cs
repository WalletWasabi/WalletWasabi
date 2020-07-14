using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace WalletWasabi.Packager
{
	public class BitnodesApi
	{
		public static void PrintOnions(TextWriter textWriter)
		{
			PrintOnions(textWriter, null);
		}

		public static void PrintOnions(TextWriter textWriter, HashSet<string> currentOnions)
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://bitnodes.21.co/api/v1/");

			using var response = httpClient.GetAsync("snapshots/latest/", HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(response.StatusCode.ToString());
			}

			var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			JObject json = (JObject)JsonConvert.DeserializeObject(responseString);

			var onions = new List<string>();
			foreach (JProperty node in json["nodes"])
			{
				if (!node.Name.Contains(".onion"))
				{
					continue;
				}

				var userAgent = ((JArray)node.Value)[1].ToString();

				try
				{
					var verString = userAgent.Substring(userAgent.IndexOf("Satoshi:") + 8, 4);
					var ver = new Version(verString);
					bool addToResult = currentOnions is null || currentOnions.Contains(node.Name);

					if (ver >= new Version("0.16") && addToResult)
					{
						onions.Add(node.Name);
					}
				}
				catch
				{
				}
			}

			foreach (var onion in onions.OrderBy(x => x))
			{
				textWriter.WriteLine(onion);
			}
		}
	}
}
