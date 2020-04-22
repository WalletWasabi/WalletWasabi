using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Gui.P2EP
{
	public class P2EPRequestHandler
	{
		public P2EPRequestHandler(Network network)
		{
			Network = network;
		}

		public Network Network { get; }

		public Task<string> HandleAsync(string body, CancellationToken cancellationToken)
		{
			NotificationHelpers.Notify("request received!!!!", "PAYJOIN", NotificationType.Information);
			if (!PSBT.TryParse(body, Network, out var psbt))
			{
				throw new P2EPException("What the heck are you trying to do?");
			}
			return Task.FromResult(body);
		}
	}
}
