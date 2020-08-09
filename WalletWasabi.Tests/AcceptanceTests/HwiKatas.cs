using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using Xunit;

namespace WalletWasabi.Tests.AcceptanceTests
{
	/// <summary>
	/// Kata tests are intended to be run one by one.
	/// A kata is a type of test that requires user interaction.
	/// User interaction shall be defined in the beginning of the each kata.
	/// Only write katas those require button push responses (eg. don't call setup on trezor.)
	/// </summary>
	public class HwiKatas
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
			// Connect and initialize your Trezor T with the following seed phrase:
			// more maid moon upgrade layer alter marine screen benefit way cover alcohol
			// Run this test.
			// displayaddress request: refuse
			// displayaddress request: confirm
			// displayaddress request: confirm
			// signtx request: confirm
			//
			// --- USER INTERACTIONS ---

			var network = Network.Main;
			var client = new HwiClient(network);
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.NotNull(entry.Path);
			Assert.Equal(HardwareWalletModels.Trezor_T, entry.Model);
			Assert.True(entry.Fingerprint.HasValue);

			string devicePath = entry.Path;
			HardwareWalletModels deviceType = entry.Model;
			HDFingerprint fingerprint = entry.Fingerprint.Value;

			await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));

			await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));

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

			// USER: CONFIRM
			// The user has to confirm multiple times because this transaction spends 22 inputs.
			// The transaction is similar to these transactions:
			// https://blockstream.info/testnet/tx/580d04a1891bf5b03a972eb63791e57ca39b85476d45f1d82a09732fe4c9214d
			// https://blockstream.info/testnet/tx/82cd8165a4fb3276354a817ad1b991a0c4af7d6d438f9052f34d58712f873457
			PSBT psbt = PSBT.Parse("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", network);
			PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

			var checkResult = signedTx.Check();
			Assert.Equal(TransactionCheckResult.Success, checkResult);
		}

		[Fact]
		public async Task TrezorOneKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect an already initialized device. Don't unlock it.
			// Run this test.
			//
			// --- USER INTERACTIONS ---

			var network = Network.Main;
			var client = new HwiClient(network);
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.NotNull(entry.Path);
			Assert.Equal(HardwareWalletModels.Trezor_1, entry.Model);
			Assert.True(entry.NeedsPinSent);
			Assert.Equal(HwiErrorCode.DeviceNotReady, entry.Code);
			Assert.Null(entry.Fingerprint);

			string devicePath = entry.Path;
			HardwareWalletModels deviceType = entry.Model;

			await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));

			await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));
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

		[Fact]
		public async Task ColdCardKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect an already initialized device and unlock it.
			// Run this test.
			// signtx request: refuse
			// signtx request: confirm
			//
			// --- USER INTERACTIONS ---

			var network = Network.Main;
			var client = new HwiClient(network);
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.NotNull(entry.Path);
			Assert.Equal(HardwareWalletModels.Coldcard, entry.Model);
			Assert.True(entry.Fingerprint.HasValue);

			string devicePath = entry.Path;
			HardwareWalletModels deviceType = entry.Model;
			HDFingerprint fingerprint = entry.Fingerprint.Value;

			// ColdCard doesn't support it.
			await Assert.ThrowsAsync<HwiException>(async () => await client.WipeAsync(deviceType, devicePath, cts.Token));

			// ColdCard doesn't support it.
			await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));

			// ColdCard doesn't support it.
			await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));

			// ColdCard doesn't support it.
			await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			// ColdCard doesn't support it.
			await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			Assert.NotNull(xpub1);
			Assert.NotNull(xpub2);
			Assert.NotEqual(xpub1, xpub2);

			PSBT psbt = BuildPsbt(network, fingerprint, xpub1, keyPath1);

			// USER: REFUSE
			var ex = await Assert.ThrowsAsync<HwiException>(async () => await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token));
			Assert.Equal(HwiErrorCode.ActionCanceled, ex.ErrorCode);

			// USER: CONFIRM
			PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

			var checkResult = signedTx.Check();
			Assert.Equal(TransactionCheckResult.Success, checkResult);

			// ColdCard just display the address. There is no confirm/refuse action.

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(fingerprint, keyPath2, cts.Token);
			Assert.NotNull(address1);
			Assert.NotNull(address2);
			Assert.NotEqual(address1, address2);
			var expectedAddress1 = xpub1.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			var expectedAddress2 = xpub2.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
		}

		[Fact]
		public async Task LedgerNanoSKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect an already initialized device and unlock it and enter to Bitcoin App.
			// Run this test.
			// displayaddress request: refuse (accept Warning messages)
			// displayaddress request: confirm
			// displayaddress request: confirm
			// signtx request: refuse
			// signtx request: confirm
			//
			// --- USER INTERACTIONS ---

			var network = Network.Main;
			var client = new HwiClient(network);
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var enumerate = await client.EnumerateAsync(cts.Token);
			HwiEnumerateEntry entry = Assert.Single(enumerate);
			Assert.NotNull(entry.Path);
			Assert.Equal(HardwareWalletModels.Ledger_Nano_S, entry.Model);
			Assert.True(entry.Fingerprint.HasValue);
			Assert.Null(entry.Code);
			Assert.Null(entry.Error);
			Assert.Null(entry.SerialNumber);
			Assert.False(entry.NeedsPassphraseSent);
			Assert.False(entry.NeedsPinSent);

			string devicePath = entry.Path;
			HardwareWalletModels deviceType = entry.Model;
			HDFingerprint fingerprint = entry.Fingerprint.Value;

			await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));

			await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));

			await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));

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
			Assert.Equal(HwiErrorCode.BadArgument, ex.ErrorCode);

			// USER: CONFIRM
			PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

			var checkResult = signedTx.Check();
			Assert.Equal(TransactionCheckResult.Success, checkResult);
		}
	}
}
