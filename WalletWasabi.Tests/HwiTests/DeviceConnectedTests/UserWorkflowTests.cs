using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2;
using WalletWasabi.Hwi2.Exceptions;
using WalletWasabi.Hwi2.Models;
using Xunit;

namespace WalletWasabi.Tests.HwiTests.DeviceConnectedTests
{
	public class UserWorkflowTests
	{
		#region SharedVariables

		// Bottleneck: user action on device.
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(1);

		#endregion SharedVariables

		[Fact]
		public async Task TrezorTTestsAsync()
		{
			// USER: Connect a Trezor T
			var client = new HwiClient(Network.Main);
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				var enumerate = await client.EnumerateAsync(cts.Token);

				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletVendors.Trezor, entry.Type);
				Assert.NotNull(entry.Path);
				Assert.NotEmpty(entry.Path);
				Assert.False(entry.NeedsPassphraseSent);
				Assert.False(entry.NeedsPinSent);
				Assert.NotNull(entry.Error);
				Assert.NotEmpty(entry.Error);
				Assert.Equal(HwiErrorCode.NotInitialized, entry.Code);
				Assert.Null(entry.Fingerprint);
			}
		}
	}
}
