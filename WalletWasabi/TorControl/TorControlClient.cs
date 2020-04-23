using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

		public async Task<string> CreateHiddenServiceAsync()
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
			using var sw = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
			sw.NewLine = "\r\n";
			await sw.WriteLineAsync(command);
			sw.Flush();

			using var sr = new StreamReader(stream, leaveOpen: true);
			var line = "";
			var key = "";
			var value = "";
			var result = new List<KeyValuePair<string, string>>();
			while(sr.Peek() >= 0)
			{
				line = sr.ReadLine();
				if (line.StartsWith("250-"))
				{
					var pos = line.IndexOf('=');
					key = line["250-".Length..pos];
					value = line[(pos+1)..];
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

			if (line != "250 OK")
			{
				throw new Exception(line);
			}

			return result;
		}
		

		public void Dispose()
		{
		}
	}

}