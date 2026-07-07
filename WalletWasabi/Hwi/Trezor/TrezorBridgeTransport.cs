using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Talks to a Trezor device through the Trezor Bridge (trezord) local HTTP API.
/// The bridge is provided by a running Trezor Suite or a standalone trezord process.
/// Messages are framed as: message type (u16, big endian) || payload length (u32, big endian) || protobuf payload,
/// hex-encoded in the HTTP body.
/// </summary>
public class TrezorBridgeTransport : IDisposable
{
	/// <summary>Standalone trezord listens on 21325, the bridge bundled in Trezor Suite on 21328.</summary>
	public static readonly string[] DefaultBridgeUris = ["http://127.0.0.1:21325", "http://127.0.0.1:21328"];

	public TrezorBridgeTransport(string bridgeUri)
	{
		_bridgeUri = bridgeUri;

		// The bridge listens on localhost only, no clearnet traffic is involved.
		SocketsHttpHandler? handler = new();
		try
		{
			_httpClient = new HttpClient(handler, disposeHandler: true)
			{
				// Device calls block until the user interacts with the device, do not time them out here.
				Timeout = Timeout.InfiniteTimeSpan
			};
			handler = null;
		}
		finally
		{
			handler?.Dispose();
		}

		// Standalone trezord rejects requests without a whitelisted origin with 403.
		_httpClient.DefaultRequestHeaders.Add("Origin", "https://wallet.trezor.io");
	}

	private readonly string _bridgeUri;
	private readonly HttpClient _httpClient;

	public record BridgeDevice(string Path, string? Session);

	public virtual async Task<IReadOnlyList<BridgeDevice>> EnumerateAsync(CancellationToken cancellationToken)
	{
		string response = await PostAsync("enumerate", "", cancellationToken).ConfigureAwait(false);
		using var json = JsonDocument.Parse(response);
		return json.RootElement.EnumerateArray()
			.Select(device => new BridgeDevice(
				device.GetProperty("path").GetString()!,
				device.GetProperty("session").ValueKind == JsonValueKind.Null ? null : device.GetProperty("session").GetString()))
			.ToList();
	}

	public virtual async Task<string> AcquireAsync(BridgeDevice device, CancellationToken cancellationToken)
	{
		string response = await PostAsync($"acquire/{device.Path}/{device.Session ?? "null"}", "", cancellationToken).ConfigureAwait(false);
		using var json = JsonDocument.Parse(response);
		return json.RootElement.GetProperty("session").GetString()!;
	}

	public virtual async Task ReleaseAsync(string session, CancellationToken cancellationToken)
	{
		await PostAsync($"release/{session}", "", cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Sends one message to the device and waits for its response, which can take as long as user interaction takes.</summary>
	public virtual async Task<TrezorMessage> CallAsync(string session, TrezorMessage message, CancellationToken cancellationToken)
	{
		byte[] frame = new byte[6 + message.Payload.Length];
		BinaryPrimitives.WriteUInt16BigEndian(frame, (ushort)message.MessageType);
		BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(2), (uint)message.Payload.Length);
		message.Payload.CopyTo(frame, 6);

		string response = await PostAsync($"call/{session}", Convert.ToHexStringLower(frame), cancellationToken).ConfigureAwait(false);

		byte[] responseFrame = Convert.FromHexString(response);
		var messageType = (TrezorMessageType)BinaryPrimitives.ReadUInt16BigEndian(responseFrame);
		return new TrezorMessage(messageType, responseFrame[6..]);
	}

	private async Task<string> PostAsync(string path, string content, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, $"{_bridgeUri}/{path}")
		{
			Content = new StringContent(content)
		};

		HttpResponseMessage response;
		try
		{
			response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (HttpRequestException e)
		{
			throw new TrezorException($"Trezor Bridge is not reachable at {_bridgeUri}. Make sure Trezor Suite or trezord is running. ({e.Message})");
		}

		using (response)
		{
			string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				throw new TrezorException($"Trezor Bridge request '{path}' failed with status {(int)response.StatusCode}: {body}");
			}
			return body;
		}
	}

	public void Dispose()
	{
		_httpClient.Dispose();
	}
}
