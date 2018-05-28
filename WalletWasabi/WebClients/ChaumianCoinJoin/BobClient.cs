﻿using NBitcoin;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.TorSocks5;
using WalletWasabi.Bases;

namespace WalletWasabi.WebClients.ChaumianCoinJoin
{
	public class BobClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public BobClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		public async Task PostOutputAsync(string roundHash, BitcoinAddress activeOutputAddress, byte[] unblindedSignature)
		{
			var request = new OutputRequest() { OutputAddress = activeOutputAddress.ToString(), SignatureHex = NBitcoin.DataEncoders.Encoders.Hex.EncodeData(unblindedSignature) };
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v1/btc/chaumiancoinjoin/output?roundHash={roundHash}", request.ToHttpStringContent()))
			{
				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					string error = await response.Content.ReadAsJsonAsync<string>();
					var errorMessage = error == null ? string.Empty : $"\n{error}";
					throw new HttpRequestException($"{response.StatusCode.ToReasonString()}{errorMessage}");
				}
			}
		}
	}
}
