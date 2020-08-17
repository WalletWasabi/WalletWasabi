using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WalletWasabi.Packager
{
	/// <summary>
	/// Implementation for Bitnodes API.
	/// </summary>
	/// <seealso href="https://bitnodes.io/api/"/>
	public class BitnodesApi
	{
		public BitnodesApi(TextWriter textWriter)
		{
			TextWriter = textWriter;
		}

		private TextWriter TextWriter { get; }

		/// <summary>
		/// Finds all Bitcoin nodes with ".onion" in their names, sorts by node names and writes to <see cref="TextWriter"/>.
		/// </summary>
		/// <returns></returns>
		public async Task PrintOnionsAsync()
		{
			await PrintOnionsAsync(currentOnions: null).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds all Bitcoin nodes with ".onion" in their names, sorts by node names and writes to <see cref="TextWriter"/>.
		/// </summary>
		/// <remarks>Allows to pre-define a set of Bitcoin node names containing ".onion".</remarks>
		/// <param name="currentOnions">Set of Bitcoin node names containing ".onion".</param>
		/// <returns></returns>
		public async Task PrintOnionsAsync(HashSet<string>? currentOnions)
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://bitnodes.21.co/api/v1/");

			// List all reachable nodes from a snapshot.
			// See https://bitnodes.io/api/#list-nodes.
			using var httpResponse = await httpClient.GetAsync("snapshots/latest/", HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);

			if (httpResponse.StatusCode != HttpStatusCode.OK)
			{
				throw new HttpRequestException(httpResponse.StatusCode.ToString());
			}

			string response = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

			ProcessResponse(response, currentOnions);
		}

		/// <summary>
		/// Internal method. Use only in tests.
		/// </summary>
		/// <param name="currentOnions">Set of Bitcoin node names containing ".onion".</param>
		public void ProcessResponse(string response, HashSet<string>? currentOnions)
		{
			JObject json = (JObject)JsonConvert.DeserializeObject(response);

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
				TextWriter.WriteLine(onion);
			}
		}
	}
}
