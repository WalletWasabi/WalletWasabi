using NBitcoin.Protocol;
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
		/// <summary>v2 onion services are always 16 characters long.</summary>
		private const int OnionV2AddressLength = 16;

		/// <summary>Prefix that is used by Bitcoin Core clients to identify themselves.</summary>
		private const string BitcoinCoreClientUserAgentPrefix = "Satoshi:";

		public BitnodesApi(TextWriter textWriter)
		{
			TextWriter = textWriter;
		}

		private TextWriter TextWriter { get; }

		/// <inheritdoc cref="PrintOnionsAsync(HashSet{string}?)"/>
		public async Task PrintOnionsAsync()
		{
			await PrintOnionsAsync(currentOnions: null).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds all Bitcoin nodes that:
		/// <list type="bullet">
		/// <item>run as v2 onion services, and</item>
		/// <item>run Bitcoin Core 0.16+ client, and</item>
		/// <item>support witness data, and</item>
		/// <item>provide <see cref="NodeServices.Network"/>.</item>
		/// </list>
		/// Consequently, it sorts them by node names and writes them to <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="currentOnions">A set of Bitcoin node names containing ".onion".</param>
		/// <remarks>Allows to pre-define a set of Bitcoin node names containing ".onion".</remarks>
		/// <returns><see cref="Task"/>.</returns>
		/// <seealso href="https://bitnodes.21.co/api/v1/snapshots/latest/"/>
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
		/// <param name="currentOnions">A set of Bitcoin node names containing ".onion".</param>
		public void ProcessResponse(string response, HashSet<string>? currentOnions)
		{
			JObject json = (JObject)JsonConvert.DeserializeObject(response);

			var onions = new List<string>();
			foreach (JProperty node in json["nodes"])
			{
				// 1) Check onion service version.
				//
				// This is to filter only onion v2 services which are 16 characters long, whereas
				// onion v3 addresses are 56 characters long.
				if (node.Name.IndexOf(".onion") != OnionV2AddressLength)
				{
					continue;
				}

				JArray value = ((JArray)node.Value);

				// 2) Check node services
				ulong services = value[3].Value<ulong>();

				// 2.a) Is witness-ready? If not, skip.
				if ((services & (ulong)NodeServices.NODE_WITNESS) == 0)
				{
					continue;
				}

				// 2.b) Is full node with whole blockchain? If not, skip.
				if ((services & (ulong)NodeServices.Network) == 0)
				{
					continue;
				}

				// 3) Accept only Bitcoin Core nodes with version >=0.16.
				try
				{
					string userAgent = value[1].Value<string>();

					// Parse "major.minor" part of version. As Bitcoin Core versions are in form "0.xx.<something>",
					// the version part is represented by 4 characters.
					string satoshiClientVersion = userAgent.Substring(userAgent.IndexOf(BitcoinCoreClientUserAgentPrefix) + "Satoshi:".Length, length: 4);
					var version = new Version(satoshiClientVersion);
					bool addToResult = currentOnions is null || currentOnions.Contains(node.Name);

					if (version >= new Version("0.16") && addToResult)
					{
						onions.Add(node.Name);
					}
				}
				catch
				{
					continue;
				}
			}

			foreach (var onion in onions.OrderBy(x => x))
			{
				TextWriter.WriteLine(onion);
			}
		}
	}
}
