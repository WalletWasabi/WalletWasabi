#if !NOSOCKET
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using NBitcoin.Socks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Gui
{
	public class TorOnlyEndpointConnector : IEnpointConnector
	{
		private DefaultEndpointConnector _defaultEndpointConnector = new DefaultEndpointConnector(); 

		public TorOnlyEndpointConnector()
		{
		}

		public async Task ConnectSocket(Socket socket, EndPoint endpoint, NodeConnectionParameters nodeConnectionParameters, CancellationToken cancellationToken)
		{
			if(!endpoint.IsTor()) 
				return;
			await _defaultEndpointConnector.ConnectSocket(socket, endpoint, nodeConnectionParameters, cancellationToken);
		}
	}
}
#endif