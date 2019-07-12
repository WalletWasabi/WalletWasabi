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
	public class HwiClientTests
	{
		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public void CanCreate(Network network)
		{
			new HwiClient(network);
		}

		public static IEnumerable<object[]> GetDifferentNetworkValues()
		{
			var networks = new List<Network>
			{
				Network.Main,
				Network.TestNet,
				Network.RegTest
			};

			foreach (Network network in networks)
			{
				yield return new object[] { network };
			}
		}

		[Fact]
		public void ConstructorThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new HwiClient(null));
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task GetVersionTestsAsync(Network network)
		{
			var client = new HwiClient(network);

			using (var cts = new CancellationTokenSource(3000))
			{
				Version version = await client.GetVersionAsync(cts.Token);
				Assert.Equal(new Version("1.0.1"), version);
			}

			using (var cts = new CancellationTokenSource(1))
			{
				await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetVersionAsync(cts.Token));
			}
		}
	}
}
