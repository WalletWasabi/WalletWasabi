using WalletWasabi.Tests.BitcoinCore;
using WalletWasabi.Tests.Helpers;

namespace WalletWasabi.Tests.XunitConfiguration;

public class RegTestFixture : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	public RegTestFixture()
	{
		IndexerRegTestNode = TestNodeBuilder.CreateAsync(callerFilePath: "RegTests", callerMemberName: "BitcoinCoreData").GetAwaiter().GetResult();

		var walletName = "wallet";
		IndexerRegTestNode.RpcClient.CreateWalletAsync(walletName).GetAwaiter().GetResult();
	}

	public CoreNode IndexerRegTestNode { get; }

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				IndexerRegTestNode.TryStopAsync().GetAwaiter().GetResult();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(true);
	}
}
