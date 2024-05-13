using System.Net.WebSockets;
using System.Threading.Channels;

namespace WalletWasabi.Backend.Middlewares;

public class WebSocketConnectionState
{
	public WebSocketConnectionState(WebSocket webSocket, DateTime connectedSince)
	{
		WebSocket = webSocket;
		ConnectedSince = connectedSince;
		MessagesToSend = Channel.CreateBounded<byte[]>(1_000);
	}
	public WebSocket WebSocket { get; }
	public DateTime ConnectedSince { get; }
	public bool Handshaked { get; set; }
	public Channel<byte[]> MessagesToSend { get; }
}
