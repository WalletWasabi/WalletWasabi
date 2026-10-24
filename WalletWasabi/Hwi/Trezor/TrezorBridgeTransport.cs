using System.Buffers.Binary;
using System.Net.Http;
using System.Text.Json;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Talks to a Trezor device through the Trezor Bridge local HTTP API (Trezor Suite's, or a standalone trezord).
/// Frames are message type (u16 BE) || payload length (u32 BE) || protobuf payload, hex-encoded in the body.
///
/// The bridge is trusted with nothing: it is unauthenticated HTTP on localhost, any local process can hold its
/// port, and the probe on "/" only tells a bridge apart from an unrelated listener. The device keeps funds safe:
/// imported account keys are proven by an address it shows, and the firmware enforces the confirmed rounds, fee
/// cap and SLIP-25 isolation on every SignTx. A process owning the port can at most spend the authorization the
/// user confirmed, within its caps - the power it already has over Trezor Suite.
/// </summary>
public class TrezorBridgeTransport : IDisposable
{
	/// <summary>Standalone trezord listens on 21325, the bridge bundled in Trezor Suite on 21328.</summary>
	public static readonly string[] DefaultBridgeUris = ["http://127.0.0.1:21325", "http://127.0.0.1:21328"];

#pragma warning disable CA2000 // Dispose objects before losing scope - the HttpClient owns the handler and disposes it.
	public TrezorBridgeTransport(string bridgeUri, HttpMessageHandler? handler = null)
	{
		_bridgeUri = bridgeUri;
		_httpClient = new HttpClient(handler ?? new SocketsHttpHandler(), disposeHandler: true)
		{
			// Device calls block until the user interacts with the device, do not time them out here.
			Timeout = Timeout.InfiniteTimeSpan
		};

		// Standalone trezord rejects requests without a whitelisted origin with 403.
		_httpClient.DefaultRequestHeaders.Add("Origin", "https://wallet.trezor.io");
	}
#pragma warning restore CA2000

	private readonly string _bridgeUri;
	private readonly HttpClient _httpClient;

	/// <summary>Whether this bridge wants device frames wrapped in a JSON envelope (trezord-node in Trezor Suite) rather than bare hex (trezord-go); null until asked.</summary>
	private bool? _protocolMessages;

	public record BridgeDevice(string Path, string? Session);

	public virtual async Task<IReadOnlyList<BridgeDevice>> EnumerateAsync(CancellationToken cancellationToken)
	{
		await EnsureBridgeAsync(cancellationToken).ConfigureAwait(false);
		string response = await PostAsync("enumerate", "", cancellationToken).ConfigureAwait(false);
		using var json = JsonDocument.Parse(response);
		return json.RootElement.EnumerateArray()
			.Select(device => new BridgeDevice(
				device.GetProperty("path").GetString()!,
				device.GetProperty("session").GetString()))
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
		return ParseFrame(response);
	}

	/// <summary>Reads one queued message without sending anything: a client-side cancel leaves its reply on the bridge, which would offset every later request by one.</summary>
	public virtual async Task<TrezorMessage> ReadAsync(string session, CancellationToken cancellationToken) =>
		ParseFrame(await PostDeviceMessageAsync($"read/{session}", null, cancellationToken).ConfigureAwait(false));

	private static TrezorMessage ParseFrame(string hexFrame)
	{
		byte[] frame = Convert.FromHexString(hexFrame);
		return new TrezorMessage((TrezorMessageType)BinaryPrimitives.ReadUInt16BigEndian(frame), frame[6..]);
	}

	/// <summary>Posts a device message frame to <paramref name="path"/> and returns the response frame, hex encoded; a null <paramref name="hexFrame"/> is for endpoints that take none, like /read.</summary>
	private async Task<string> PostDeviceMessageAsync(string path, string? hexFrame, CancellationToken cancellationToken)
	{
		if (!await EnsureBridgeAsync(cancellationToken).ConfigureAwait(false))
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
	/// Asks the bridge once who it is, so a stray process on the port fails here rather than inside the device protocol;
	/// returns its protocolMessages flag (JSON envelope on /call and /read, which trezord-node in Trezor Suite requires).
	/// </summary>
	private async Task<bool> EnsureBridgeAsync(CancellationToken cancellationToken)
	{
		if (_protocolMessages is { } known)
		{
			return known;
		}

		string response = await PostAsync("", "", cancellationToken).ConfigureAwait(false);
		try
		{
			using var json = JsonDocument.Parse(response);
			if (json.RootElement.ValueKind is JsonValueKind.Object
				&& json.RootElement.TryGetProperty("version", out var version)
				&& version.ValueKind is JsonValueKind.String)
			{
				_protocolMessages = json.RootElement.TryGetProperty("protocolMessages", out var flag) && flag.ValueKind is JsonValueKind.True;
				return _protocolMessages.Value;
			}
		}
		catch (JsonException)
		{
		}

		throw new TrezorException($"Something answers at {_bridgeUri}, but it is not a Trezor Bridge.");
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
