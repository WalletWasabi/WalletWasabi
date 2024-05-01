using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Backend.Middlewares;

/// <summary>
/// WebSocketHandlerBase is the base class for all WebSocketHandlers.
/// WebSocketHandlers are referenced and called by the WebSocketHandlerMiddleware instance
/// </summary>
/// <param name="connectionTracker">The instance that keeps track of all websockets.</param>
public abstract class WebSocketHandlerBase(WebSocketsConnectionTracker connectionTracker)
{
	/// <summary>
	/// OnConnectAsync is called by the WebSocketHandlerMiddleware instance every time a new
	/// websocket connection is accepted.
	/// </summary>
	/// <param name="socket">The websocket instance.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public virtual Task OnConnectedAsync(WebSocket socket, CancellationToken cancellationToken)
	{
		var channel = connectionTracker.AddSocket(socket);
		Task.Run(async () =>
		{
			await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				await socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken)
					.ConfigureAwait(false);
			}
		}, cancellationToken);
		return Task.CompletedTask;
	}

	/// <summary>
	/// OnDisconnectedAsync is called by the WebSocketHandlerMiddleware instance every time
	/// a websocket starts the closing handshake or simply closed the connection unilaterally.
	/// </summary>
	/// <param name="socket">The web socket.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public virtual Task OnDisconnectedAsync(WebSocket socket, CancellationToken cancellationToken)
	{
		var connectionState = connectionTracker.GetWebSocketConnectionState(socket);
		connectionState.MessagesToSend.Writer.Complete();

		connectionTracker.RemoveSocket(socket);
		return socket.State is WebSocketState.Open
			? socket.CloseAsync(
				WebSocketCloseStatus.NormalClosure,
				$"Closed by the {nameof(WebSocketHandlerBase)}",
				cancellationToken)
			: Task.CompletedTask;
	}

	public async Task SendAsync(WebSocket socket, byte[] message, CancellationToken cancellationToken)
	{
		var connectionState = connectionTracker.GetWebSocketConnectionState(socket);
		await connectionState.MessagesToSend.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Receives
	/// </summary>
	/// <param name="socket">The websocket.</param>
	/// <param name="result">The websocket reading result.</param>
	/// <param name="buffer">The buffer containing the read message</param>
	/// <param name="cancellationToken">The cancellationToken.</param>
	public virtual Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer, CancellationToken cancellationToken) =>
		ReceiveAsync(connectionTracker.GetWebSocketConnectionState(socket), result, buffer, cancellationToken);

	public abstract Task ReceiveAsync(WebSocketConnectionState socketState, WebSocketReceiveResult result, byte[] buffer, CancellationToken cancellationToken);
}
