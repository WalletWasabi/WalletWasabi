using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services.Events;
using WalletWasabi.Stores;
using WalletWasabi.Synchronization;

namespace WalletWasabi.Services;

public class SatoshiSynchronizer : BackgroundService
{
	private readonly BitcoinStore _bitcoinStore;
	private readonly Uri _satoshiEndpointUri;
	private readonly Uri? _socksProxyUri;
	private readonly EventBus _eventBus;

	public SatoshiSynchronizer(BitcoinStore bitcoinStore, Uri satoshiEndpointUri, EndPoint? socksEndPoint, EventBus eventBus)
	{
		_bitcoinStore = bitcoinStore;
		_satoshiEndpointUri = satoshiEndpointUri;
		_eventBus = eventBus;
		_socksProxyUri = socksEndPoint switch
		{
			DnsEndPoint dns => new UriBuilder("socks5", dns.Host, dns.Port).Uri,
			IPEndPoint ip => new UriBuilder("socks5", ip.Address.ToString(), ip.Port).Uri,
			null => null,
			_ => throw new NotSupportedException("The endpoint type is not supported.")
		};
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		var localChain = _bitcoinStore.SmartHeaderChain;
		while (!cancellationToken.IsCancellationRequested)
		{
			using var ws = new ClientWebSocket();
			try
			{
				await ConnectToSatoshiEndpointAsync(ws).ConfigureAwait(false);
				await StartReceivingMessagesAsync(ws).ConfigureAwait(false);
			}
			catch (WebSocketException)
			{
				_eventBus.Publish(new ConnectionStateChanged(false));
				await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Swallow it and try again, or break is cancellation was requested.
			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
			finally
			{
				_eventBus.Publish(new ConnectionStateChanged(false));
				await DisconnectFromSatoshiEndpointAsync(ws).ConfigureAwait(false);
			}
		}

		return;

		async Task ConnectToSatoshiEndpointAsync(ClientWebSocket ws)
		{
			ws.Options.Proxy = _socksProxyUri is not null ? new WebProxy(_socksProxyUri) : ws.Options.Proxy;
			await ws.ConnectAsync(_satoshiEndpointUri, cancellationToken).ConfigureAwait(false);

			_eventBus.Publish(new ConnectionStateChanged(true));

			await WaitForTipHashAsync().ConfigureAwait(false);
			await HandshakeAsync(localChain.TipHash).ConfigureAwait(false);
			return;

			async Task WaitForTipHashAsync()
			{
				while (localChain.TipHash is null) // Just another hidden communication/synchronization channel
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
				}
			}

			async Task HandshakeAsync(uint256 tipHash)
			{
				var handshake = new HandshakeMessage(tipHash);
				await ws.SendAsync(handshake.ToByteArray(), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
			}
		}

		async Task DisconnectFromSatoshiEndpointAsync(WebSocket ws)
		{
			if (ws.State == WebSocketState.Open)
			{
				try
				{
					await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", cancellationToken)
						.ConfigureAwait(false);
				}
				catch
				{
					// ignored
				}
			}
		}

		async Task StartReceivingMessagesAsync(WebSocket ws)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var buffer = new byte[80 * 1024];
				var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					return;
				}

				var messageType = (ResponseMessage)buffer[0];
				using var reader = new BinaryReader(new MemoryStream(buffer[1..]));
				switch (messageType)
				{
					case ResponseMessage.BlockHeight:
						var height = reader.ReadUInt32();
						localChain.SetServerTipHeight(height);
						_eventBus.Publish(new ServerTipHeightChanged(height));
						break;

					case ResponseMessage.SoftwareVersion:
						var clientVersion = reader.ReadVersion();
						var serverVersion = reader.ReadVersion();
						_eventBus.Publish(new SoftwareVersionChanged(clientVersion, serverVersion));
						break;

					case ResponseMessage.LegalDocumentVersion:
						var version = reader.ReadVersion();
						_eventBus.Publish(new LegalDocumentVersionChanged(version));
						break;

					case ResponseMessage.Filter:
						var filter = reader.ReadFilterModel();
						if (localChain.TipHeight + 1 != filter.Header.Height)
						{
							Logger.LogInfo(ChainHeightMismatchError(filter));
							await RewindAsync(1).ConfigureAwait(false);
							return;
						}

						await _bitcoinStore.IndexStore.AddNewFiltersAsync([filter]).ConfigureAwait(false);
						break;

					case ResponseMessage.HandshakeError:
						await RewindAsync(144).ConfigureAwait(false);
						return;

					case ResponseMessage.ExchangeRate:
						var exchangeRate = reader.ReadDecimal();
						_eventBus.Publish(new ExchangeRateChanged(exchangeRate));
						break;

					case ResponseMessage.MiningFeeRates:
						var allFeeEstimate = reader.ReadMiningFeeRates();
						_eventBus.Publish(new MiningFeeRatesChanged(FeeRateSource.Backend, allFeeEstimate));
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		async Task RewindAsync(int count)
		{
			var rewindCount = Math.Min(count, localChain.HashCount);
			var rewindTipHeight = Math.Max(0, localChain.TipHeight - rewindCount);
			await _bitcoinStore.IndexStore.RemoveAllNewerThanAsync((uint) rewindTipHeight).ConfigureAwait(false);
		}

		string ChainHeightMismatchError(FilterModel filter)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Inconsistent index state detected.");
			sb.Append($"Local chain: {localChain.TipHeight}/{localChain.ServerTipHeight} ({localChain.HashesLeft} left");
			sb.AppendLine($"- best known block hash: {localChain.TipHash}");
			sb.AppendLine($"Received filter: {filter.Header.BlockHash} height: {filter.Header.Height}");
			return sb.ToString();
		}
	}
}
