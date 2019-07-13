using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2;
using Xunit;

namespace WalletWasabi.Tests.HwiTests
{
	public class TrezorOneTests
	{
		#region SharedVariables

		// Bottleneck: Windows CI.
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(1);

		public HwiClient HwiClient { get; } = new HwiClient(Network.TestNet, new IMockHwiProcessBridge());

		#endregion SharedVariables

		#region Tests

		[Fact]
		public async Task CanEnumerateTestsAsync()
		{
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<string> enumerate = await HwiClient.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
			}
		}

		#endregion Tests
	}
}
