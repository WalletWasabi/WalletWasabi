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

	// The bridge listens on localhost only, no clearnet traffic is involved.
#pragma warning disable CA2000 // Dispose objects before losing scope - the HttpClient owns the handler and disposes it.
	public TrezorBridgeTransport(string bridgeUri)
		: this(bridgeUri, new SocketsHttpHandler())
	{
	}
#pragma warning restore CA2000

	internal TrezorBridgeTransport(string bridgeUri, HttpMessageHandler handler)
	{
		_bridgeUri = bridgeUri;
		_httpClient = CreateHttpClient(handler);
	}

	private static HttpClient CreateHttpClient(HttpMessageHandler handler)
	{
		var httpClient = new HttpClient(handler, disposeHandler: true)
		{
			// Device calls block until the user interacts with the device, do not time them out here.
			Timeout = Timeout.InfiniteTimeSpan
		};

		// Standalone trezord rejects requests without a whitelisted origin with 403.
		httpClient.DefaultRequestHeaders.Add("Origin", "https://wallet.trezor.io");
		return httpClient;
	}

	private readonly string _bridgeUri;
	private readonly HttpClient _httpClient;
	private BridgeWireFormat _wireFormat = BridgeWireFormat.Unknown;

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

		string response = await PostDeviceMessageAsync($"call/{session}", Convert.ToHexStringLower(frame), cancellationToken).ConfigureAwait(false);

		byte[] responseFrame = Convert.FromHexString(response);
		var messageType = (TrezorMessageType)BinaryPrimitives.ReadUInt16BigEndian(responseFrame);
		return new TrezorMessage(messageType, responseFrame[6..]);
	}

	/// <summary>
	/// Reads one queued message from the device without sending anything. A call that was canceled
	/// client-side leaves its reply queued on the bridge; the next request would read that stale reply
	/// and be off by one forever. Draining with bare reads on session open restores the pairing.
	/// </summary>
	public virtual async Task<TrezorMessage> ReadAsync(string session, CancellationToken cancellationToken)
	{
		string response = await PostDeviceMessageAsync($"read/{session}", null, cancellationToken).ConfigureAwait(false);
		byte[] responseFrame = Convert.FromHexString(response);
		var messageType = (TrezorMessageType)BinaryPrimitives.ReadUInt16BigEndian(responseFrame);
		return new TrezorMessage(messageType, responseFrame[6..]);
	}

	/// <summary>
	/// Sends a device message frame to <paramref name="path"/> and returns the response frame, hex encoded.
	/// A <see langword="null"/> <paramref name="hexFrame"/> means the endpoint takes no frame, like /read.
	/// </summary>
	private async Task<string> PostDeviceMessageAsync(string path, string? hexFrame, CancellationToken cancellationToken)
	{
		if (await GetWireFormatAsync(cancellationToken).ConfigureAwait(false) is BridgeWireFormat.RawHex)
		{
			return await PostAsync(path, hexFrame ?? "", cancellationToken).ConfigureAwait(false);
		}

		// The frame is hex, so it needs no escaping. "bridge" keeps the framing this class already speaks:
		// the bridge translates it to and from the codec the device wants.
		string body = hexFrame is null
			? """{"protocol":"bridge"}"""
			: $$"""{"protocol":"bridge","data":"{{hexFrame}}"}""";

		string response = await PostAsync(path, body, cancellationToken).ConfigureAwait(false);
		using var json = JsonDocument.Parse(response);
		return json.RootElement.TryGetProperty("data", out var data) ? data.GetString() ?? "" : "";
	}

	/// <summary>
	/// Asks the bridge once which wire format its /call and /read endpoints take. trezord-go accepts the
	/// bare hex frame; the trezord-node bundled in Trezor Suite only accepts it wrapped in a JSON envelope
	/// and answers 400 otherwise. It announces that with the protocolMessages flag.
	/// </summary>
	private async Task<BridgeWireFormat> GetWireFormatAsync(CancellationToken cancellationToken)
	{
		if (_wireFormat is not BridgeWireFormat.Unknown)
		{
			return _wireFormat;
		}

		string response = await PostAsync("", "", cancellationToken).ConfigureAwait(false);
		using var json = JsonDocument.Parse(response);
		_wireFormat = json.RootElement.TryGetProperty("protocolMessages", out var flag) && flag.ValueKind == JsonValueKind.True
			? BridgeWireFormat.ProtocolMessage
			: BridgeWireFormat.RawHex;

		return _wireFormat;
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

	/// <summary>How the bridge wants device message frames on its /call and /read endpoints.</summary>
	private enum BridgeWireFormat
	{
		Unknown,

		/// <summary>trezord-go: the hex frame is the whole body.</summary>
		RawHex,

		/// <summary>trezord-node, bundled in Trezor Suite: the hex frame goes in a JSON envelope.</summary>
		ProtocolMessage
	}
}
