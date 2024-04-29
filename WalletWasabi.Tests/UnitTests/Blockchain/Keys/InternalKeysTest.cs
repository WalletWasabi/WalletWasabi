using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Keys;

public class InternalKeysTest
{
	[Fact]
	public void InternalKeys()
	{
		var rpc = new MockRpcClient();
		var testWallet = new TestWallet("random-wallet", rpc);

		WabiSabiConfig config = new();
		config.CoordinatorExtPubKey = testWallet.GetSegwitAccountExtPubKey();

		for (int index = 0; index < 10; index++)
		{
			var coordinatorInternalScript = config.DeriveCoordinatorScript(index);
			var walletInternalScript = testWallet.CreateNewAddress(isInternal: true).ScriptPubKey;

			Assert.Equal(coordinatorInternalScript, walletInternalScript);
		}
	}
}
