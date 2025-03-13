using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;

namespace WalletWasabi.BitcoinRpc;

public class RpcMonitor : PeriodicRunner
{
	private RpcStatus _rpcStatus;

	public RpcMonitor(TimeSpan period, IRPCClient rpcClient) : base(period)
	{
		_rpcStatus = RpcStatus.Connecting;
		RpcClient = rpcClient;
	}

	public event EventHandler<RpcStatus>? RpcStatusChanged;

	public IRPCClient RpcClient { get; set; }

	public RpcStatus RpcStatus
	{
		get => _rpcStatus;
		private set
		{
			if (value != _rpcStatus)
			{
				_rpcStatus = value;
				RpcStatusChanged?.Invoke(this, value);
			}
		}
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		RpcStatus = await RpcClient.GetRpcStatusAsync(cancel).ConfigureAwait(false);
	}
}
