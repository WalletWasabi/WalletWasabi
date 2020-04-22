using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.TorControl
{
	/// <summary>
	/// Create an instance with the TorSocks5Manager
	/// </summary>
	public class TorControlClient : IDisposable
	{
		private TcpClient _client;

		public TorControlClient()
		{
			_client = new TcpClient();
		}

		public Task ConnectAsync()
		{
			return _client.ConnectAsync(IPAddress.Loopback, Constants.DefaultTorControlPort);
		}


		public Task AuthenticateAsync(string password)
		{
			// 16:F44A5EEADC1D0EDC6074748B610587E5348B2914231E9292E2F4C8B1DF
			return SendControlCommandAsync($"AUTHENTICATE \"{password}\"");
		}

		public async Task<string> CreateHiddenService()
		{
			var result = await SendControlCommandAsync($"ADD_ONION NEW:BEST Flags=DiscardPK Port={37129},{IPAddress.Loopback}:{37129}").ConfigureAwait(false);
			return result.First(x => x.Key == "ServiceID").Value;
		}

		public Task DestroyHiddenService(string serviceId)
		{
			return SendControlCommandAsync($"DEL_ONION {serviceId}");
		}

		public async Task<List<KeyValuePair<string, string>>> SendControlCommandAsync(string command)
		{
			var stream = _client.GetStream();
			using var sw = new StreamWriter(stream);
			await sw.WriteLineAsync(command);
			sw.Flush();

			using var sr = new StreamReader(stream);
			var response = await sr.ReadToEndAsync();

			var lines = response.Split("\r\n");
			if (lines[^1] != "250 OK")
			{
				throw new Exception(response);
			}

			var key = "";
			var value = "";
			var result = new List<KeyValuePair<string, string>>();
			foreach(var line in lines[0..^1])
			{
				if (line.StartsWith("250-"))
				{
					var pos = line.IndexOf('=');
					key = line["250-".Length..pos];
					value = line[pos..];
				}
				else if (!string.IsNullOrEmpty(key))
				{
					value = line;
				}
				if (!string.IsNullOrEmpty(value))
				{
					result.Add(new KeyValuePair<string, string>(key, value));
					(key, value) = ("", "");
				}
			}
			return result;
		}
		

		public void Dispose()
		{
		}
	}

}