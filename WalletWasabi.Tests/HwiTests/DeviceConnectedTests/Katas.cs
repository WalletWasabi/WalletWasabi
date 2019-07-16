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
			// signtx request: refuse
			// signtx request: confirm
			//
			// --- USER INTERACTIONS ---

			var network = Network.Main;
			var client = new HwiClient(network);
			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				var enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.NotNull(entry.Path);
				Assert.True(entry.Type.HasValue);
				Assert.True(entry.Fingerprint.HasValue);

				string devicePath = entry.Path;
				HardwareWalletVendors deviceType = entry.Type.Value;
				HDFingerprint fingerprint = entry.Fingerprint.Value;

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
				BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(fingerprint, keyPath2, cts.Token);
				Assert.NotNull(address1);
				Assert.NotNull(address2);
				Assert.NotEqual(address1, address2);
				var expectedAddress1 = xpub1.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				var expectedAddress2 = xpub2.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);

				// USER: REFUSE
				PSBT psbt = BuildPsbt(network, fingerprint, xpub1, keyPath1);
				var ex = await Assert.ThrowsAsync<HwiException>(async () => await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token));
				Assert.Equal(HwiErrorCode.ActionCanceled, ex.ErrorCode);

				// USER: CONFIRM
				PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token);

				Transaction signeTx = signedPsbt.GetOriginalTransaction();
				Assert.Equal(psbt.GetOriginalTransaction().GetHash(), signeTx.GetHash());

				var checkResult = signeTx.Check();
				Assert.Equal(TransactionCheckResult.Success, checkResult);

				// ToDo: --fingerprint
				// ToDo: --password
			}
		}

		private static PSBT BuildPsbt(Network network, HDFingerprint fingerprint, ExtPubKey xpub, KeyPath xpubKeyPath)
		{
			var deriveSendFromKeyPath = new KeyPath("1/0");
			var deriveSendToKeyPath = new KeyPath("0/0");

			KeyPath sendFromKeyPath = xpubKeyPath.Derive(deriveSendFromKeyPath);
			KeyPath sendToKeyPath = xpubKeyPath.Derive(deriveSendToKeyPath);

			PubKey sendFromPubKey = xpub.Derive(deriveSendFromKeyPath).PubKey;
			PubKey sendToPubKey = xpub.Derive(deriveSendToKeyPath).PubKey;

			BitcoinAddress sendFromAddress = sendFromPubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			BitcoinAddress sendToAddress = sendToPubKey.GetAddress(ScriptPubKeyType.Segwit, network);

			TransactionBuilder builder = network.CreateTransactionBuilder();
			builder = builder.AddCoins(new Coin(uint256.One, 0, Money.Coins(1), sendFromAddress.ScriptPubKey));
			builder.Send(sendToAddress.ScriptPubKey, Money.Coins(0.99999m));
			PSBT psbt = builder
				.SendFees(Money.Coins(0.00001m))
				.BuildPSBT(false);

			var rootKeyPath1 = new RootedKeyPath(fingerprint, sendFromKeyPath);
			var rootKeyPath2 = new RootedKeyPath(fingerprint, sendToKeyPath);

			psbt.AddKeyPath(sendFromPubKey, rootKeyPath1, sendFromAddress.ScriptPubKey);
			psbt.AddKeyPath(sendToPubKey, rootKeyPath2, sendToAddress.ScriptPubKey);
			return psbt;
		}
	}
}
