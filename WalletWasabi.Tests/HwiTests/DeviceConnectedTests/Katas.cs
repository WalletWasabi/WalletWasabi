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
using WalletWasabi.KeyManagement;
using Xunit;

namespace WalletWasabi.Tests.HwiTests.DeviceConnectedTests
{
	/// <summary>
	/// Kata tests are intended to be run one by one.
	/// A kata is a type of test that requires user interaction.
	/// User interaction shall be defined in the beginning of the each kata.
	/// Only write katas those require button push responses (eg. don't call setup on trezor.)
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
			// Connect an already initialized device and unlock it.
			// Run this test.
			// displayaddress request: refuse
			// displayaddress request: confirm
			// displayaddress request: confirm
			//
			// --- USER INTERACTIONS ---

			var client = new HwiClient(Network.Main);
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				var enumerate = await client.EnumerateAsync(cts.Token);
				HwiEnumerateEntry entry = enumerate.Single();

				string devicePath = entry.Path;
				HardwareWalletVendors deviceType = entry.Type.Value;

				await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, cts.Token));

				await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, cts.Token));

				// Trezor T doesn't support it.
				await Assert.ThrowsAsync<HwiException>(async () => await client.BackupAsync(deviceType, devicePath, cts.Token));

				// Trezor T doesn't support it.
				await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
				// Trezor T doesn't support it.
				await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));

				KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
				KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
				ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
				ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
				Assert.NotNull(xpub1);
				Assert.NotNull(xpub2);
				Assert.NotEqual(xpub1, xpub2);

				// USER SHOULD REFUSE ACTION
				await Assert.ThrowsAsync<HwiException>(async () => await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token));

				// USER: CONFIRM
				BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
				// USER: CONFIRM
				BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);
				Assert.NotNull(address1);
				Assert.NotNull(address2);
				Assert.NotEqual(address1, address2);
				var expectedAddress1 = xpub1.PubKey.GetAddress(ScriptPubKeyType.Segwit, client.Network);
				var expectedAddress2 = xpub2.PubKey.GetAddress(ScriptPubKeyType.Segwit, client.Network);
				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);

				// ToDo: signmessage
				// ToDo: signtx
				// ToDo: --fingerprint
				// ToDo: --password
			}
		}
	}
}
