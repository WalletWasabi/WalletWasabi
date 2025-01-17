using Avalonia.Data.Converters;
using System.Globalization;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters;

public static class StatusConverters
{
	public static readonly IValueConverter TorStatusToString =
		new FuncValueConverter<TorStatus, string>(x => x switch
		{
			TorStatus.Running => Lang.Resources.Status_Tor_Running,
			TorStatus.NotRunning => Lang.Resources.Status_Tor_NotRunning,
			TorStatus.TurnedOff => Lang.Resources.Status_Tor_TurnedOff,
			{ } => x.ToString()
		});

	public static readonly IValueConverter BackendStatusToString =
		new FuncValueConverter<BackendStatus, string>(x => x switch
		{
			BackendStatus.Connected => Lang.Resources.Status_Backend_Connected,
			BackendStatus.NotConnected => Lang.Resources.Status_Backend_NotConnected,
			{ } => x.ToString()
		});

	public static readonly IValueConverter RpcStatusStringConverter =
		new FuncValueConverter<RpcStatus?, string>(status => status is null ? RpcStatus.Unresponsive.ToString() : status.ToString());

	public static readonly IValueConverter PeerStatusStringConverter =
		new FuncValueConverter<int, string>(peerCount => string.Format(Lang.Resources.Culture, Lang.Resources.Status_Peers_ConnectedCount, peerCount));
}
