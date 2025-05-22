using Avalonia.Data.Converters;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Converters;

public static class StatusConverters
{
	public static readonly IValueConverter TorStatusToString =
		new FuncValueConverter<TorStatus, string>(x => x switch
		{
			TorStatus.Running => "is running",
			TorStatus.NotRunning => "is not running",
			TorStatus.TurnedOff => "is turned off",
			{ } => x.ToString()
		});

	public static readonly IValueConverter IndexerStatusToString =
		new FuncValueConverter<IndexerStatus, string>(x => x switch
		{
			IndexerStatus.Connected => "is connected",
			IndexerStatus.NotConnected => "is not connected",
			{ } => x.ToString()
		});

	public static readonly IValueConverter FeeRateToString =
		new FuncValueConverter<int, string>(x => x == 0 ? "No data" : $"{x} s/vB");

	public static readonly IValueConverter BlockchainTipToString =
		new FuncValueConverter<uint, string>(x => x == 0 ? "No data" : $"{x:N0}");

	public static readonly IValueConverter RpcStatusStringConverter =
		new FuncValueConverter<Result<ConnectedRpcStatus,string>, string>(status => status.Match(s => s.ToString(), e => e));
}
