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
			// --- USER INTERACTIONS ---
			//
			// Connect a the device.
			// Run this test.
			// Wipe request: confirm.
			// Wipe request: confirm.
			// Wipe request: pull out the device.
			//
			// --- USER INTERACTIONS ---

			var client = new HwiClient(Network.Main);
			string devicePath;
			HardwareWalletVendors deviceType;
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				var enumerate = await client.EnumerateAsync(cts.Token);
				HwiEnumerateEntry entry = enumerate.Single();
				devicePath = entry.Path;
				deviceType = entry.Type.Value;

				// User should confirm the device action here.
				await client.WipeAsync(deviceType, devicePath, cts.Token);

				enumerate = await client.EnumerateAsync(cts.Token);

				Assert.Single(enumerate);
				entry = enumerate.Single();
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

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.WipeAsync(deviceType, null, cts.Token));
				await Assert.ThrowsAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, "", cts.Token));
				await Assert.ThrowsAsync<ArgumentException>(async () => await client.WipeAsync(deviceType, " ", cts.Token));

				// User should confirm the device action here.
				await client.WipeAsync(deviceType, devicePath, cts.Token);

				// User should make it fail by plug out the device.
				await Assert.ThrowsAsync<HwiException>(async () => await client.WipeAsync(deviceType, devicePath, cts.Token));
			}
		}
	}
}
