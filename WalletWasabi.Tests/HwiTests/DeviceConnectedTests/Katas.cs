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
	/// <summary>
	/// Kata tests are intended to be run one by one.
	/// A kata is a type of test that requires user interaction.
	/// User interaction shall be defined in the beginning of the each kata.
	/// </summary>
	public class Katas
	{
		#region SharedVariables

		// Bottleneck: user action on device.
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(3);

		#endregion SharedVariables

		[Fact]
		public async Task TrezorTKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect a the device and unlock if it's not wiped.
			// Run this test.
			// Wipe request: confirm.
			// Wipe request: confirm.
			// Setup request: refuse.
			// Setup request: confirm and setup.
			// Wipe request: confirm.
			//
			// --- USER INTERACTIONS ---

			var client = new HwiClient(Network.Main);
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				var enumerate = await client.EnumerateAsync(cts.Token);
				HwiEnumerateEntry entry = enumerate.Single();

				string devicePath = entry.Path;
				HardwareWalletVendors deviceType = entry.Type.Value;

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
				Assert.Equal(HwiErrorCode.DeviceNotInitialized, entry.Code);
				Assert.Null(entry.Fingerprint);

				// User should confirm the device action here.
				await client.WipeAsync(deviceType, devicePath, cts.Token);

				// User should refuse the device action here.
				await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, cts.Token));

				// User should confirm the device action and setup a new device here.
				await client.SetupAsync(deviceType, devicePath, cts.Token);

				// User should confirm the device action here.
				await client.WipeAsync(deviceType, devicePath, cts.Token);

				// ToDo: Restore
				// ToDo: Backup
				// ToDo: getxpub
				// ToDo: displayaddress
				// ToDo: signmessage
				// ToDo: signtx
			}
		}
	}
}
