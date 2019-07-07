using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Tests
{
	public static class Extensions
	{
		class DisposableNode : IDisposable
		{
			private readonly NBitcoin.Tests.CoreNode _coreNode;
			public DisposableNode(NBitcoin.Tests.CoreNode coreNode)
			{
				_coreNode = coreNode;
			}

			public void Dispose()
			{
				_coreNode.CreateRPCClient().Stop();
				_coreNode.WaitForExit();
			}
		}

		public static IDisposable AsDisposable(this NBitcoin.Tests.CoreNode coreNode)
		{
			return new DisposableNode(coreNode);
		}
	}
}
