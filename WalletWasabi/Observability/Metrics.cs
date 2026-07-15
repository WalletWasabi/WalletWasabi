namespace WalletWasabi.Observability;

public static class Metrics
{
	public const string P2pConnectedCounter = "p2p.connected.gauge";
	public const string P2pConnectionAttemptsCounter = "p2p.connection.attempt.count";
	public const string P2pConnectionSuccessCounter = "p2p.connection.success.count";
	public const string P2pConnectionHandshakeDurationHistogram = "p2p.connection.handshake.duration.histogram";
	public const string P2pConnectionTotalDurationHistogram = "p2p.connection.total.duration.histogram";
	public const string P2pMisbehaviorCounter = "p2p.misbehavior.count";
	public const string P2pMisbehaviorInvalidDataCounter = "p2p.misbehavior.invalid.data.count";
	public const string P2pMisbehaviorTimeoutBlockDownloadCounter = "p2p.misbehavior.timeout.block.download.count";

	public static string GetDescription(string metricName) => metricName switch
	{
		P2pConnectedCounter => "Currently connected nodes",
		P2pConnectionAttemptsCounter => "Total connection attempts",
		P2pConnectionSuccessCounter => "Successful connections",
		P2pConnectionHandshakeDurationHistogram => "Connection handshake duration in milliseconds",
		P2pConnectionTotalDurationHistogram => "Total connection duration in milliseconds",
		P2pMisbehaviorCounter => "Total misbehaviors",
		P2pMisbehaviorInvalidDataCounter => "Misbehaviors due to invalid data",
		P2pMisbehaviorTimeoutBlockDownloadCounter => "Misbehaviors due to block download timeout",
		_ => "",
	};

	public static string GetUnit(string metricName) => metricName switch
	{
		P2pConnectionHandshakeDurationHistogram => "ms",
		P2pConnectionTotalDurationHistogram => "ms",
		_ => "",
	};
}
