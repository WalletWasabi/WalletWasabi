using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2;
using WalletWasabi.Hwi2.Models;
using Xunit;

namespace WalletWasabi.Tests.HwiTests.NoDeviceConnectedTests
{
	public class MockedDeviceTests
	{
		#region SharedVariables

		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(3);

		#endregion SharedVariables

		#region Tests

		[Fact]
		public async Task CanEnumerateTestsAsync()
		{
			var client = new HwiClient(Network.Main, new IMockHwiProcessBridge(AllHardwareWallets.TrezorT));
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<string> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
			}
		}

		#endregion Tests
	}
}
