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

namespace WalletWasabi.Tests.HwiTests.NoDeviceConnectedTests
{
	public class MockedDeviceTests
	{
		#region SharedVariables

		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(3);

		#endregion SharedVariables

		#region Tests

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task TrezorTMockTestsAsync(Network network)
		{
			var client = new HwiClient(network, new IMockHwiProcessBridge(HardwareWalletModels.TrezorT));

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletVendors.Trezor, entry.Type);
				Assert.Equal("webusb: 001:4", entry.Path);
				Assert.False(entry.NeedsPassphraseSent);
				Assert.False(entry.NeedsPinSent);
				Assert.NotNull(entry.Error);
				Assert.NotEmpty(entry.Error);
				Assert.Equal(HwiErrorCode.DeviceNotInitialized, entry.Code);
				Assert.Null(entry.Fingerprint);

				var deviceType = entry.Type.Value;
				var devicePath = entry.Path;

				await client.WipeAsync(deviceType, devicePath, cts.Token);
				await client.SetupAsync(deviceType, devicePath, cts.Token);
				await client.RestoreAsync(deviceType, devicePath, cts.Token);

				// Trezor T doesn't support it.
				var backup = await Assert.ThrowsAsync<HwiException>(async () => await client.BackupAsync(deviceType, devicePath, cts.Token));

				// Trezor T doesn't support it.
				var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));

				var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			}
		}

		#endregion Tests

		#region HelperMethods

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

		#endregion HelperMethods
	}
}
