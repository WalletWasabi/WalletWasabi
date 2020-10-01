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
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(10);

		// The transaction is similar to these transactions:
		// https://blockstream.info/testnet/tx/580d04a1891bf5b03a972eb63791e57ca39b85476d45f1d82a09732fe4c9214d
		// https://blockstream.info/testnet/tx/82cd8165a4fb3276354a817ad1b991a0c4af7d6d438f9052f34d58712f873457
		public PSBT Psbt => PSBT.Parse("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", Network.Main);

		#endregion SharedVariables

		[Fact]
		public async Task TrezorTKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect and initialize your Trezor T with the following seed phrase:
			// more maid moon upgrade layer alter marine screen benefit way cover alcohol
			// Run this test.
			// displayaddress request: refuse 1 time
			// displayaddress request: confirm 2 times
			// displayaddress request: confirm 1 time
			// signtx request: confirm 23 times + Hold to confirm
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

			// USER: CONFIRM 2 TIMES
			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			// USER: CONFIRM 1 TIME
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(fingerprint, keyPath2, cts.Token);
			Assert.NotNull(address1);
			Assert.NotNull(address2);
			Assert.NotEqual(address1, address2);
			var expectedAddress1 = xpub1.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			var expectedAddress2 = xpub2.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);

			// USER: CONFIRM 23 TIMES + Hold to confirm
			// The user has to confirm multiple times because this transaction spends 22 inputs.
			PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, Psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(Psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

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

		[Fact]
		public async Task ColdCardKataAsync()
		{
			// --- USER INTERACTIONS ---
			//
			// Connect and initialize your Coldcard with the following seed phrase:
			// more maid moon upgrade layer alter marine screen benefit way cover alcohol
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

			// USER: REFUSE
			var ex = await Assert.ThrowsAsync<HwiException>(async () => await client.SignTxAsync(deviceType, devicePath, Psbt, cts.Token));
			Assert.Equal(HwiErrorCode.ActionCanceled, ex.ErrorCode);

			// USER: CONFIRM
			PSBT signedPsbt = await client.SignTxAsync(deviceType, devicePath, Psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(Psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

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
			// Connect and initialize your Nano S with the following seed phrase:
			// more maid moon upgrade layer alter marine screen benefit way cover alcohol
			// Run this test.
			// displayaddress request(derivation path): approve
			// displayaddress request: reject
			// displayaddress request(derivation path): approve
			// displayaddress request: approve
			// displayaddress request(derivation path): approve
			// displayaddress request: approve
			// signtx request: reject
			// signtx request: accept
			// confirm transaction: accept and send
			// unverified inputs: continue
			// signtx request: accept
			// confirm transaction: accept and send
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
			var ex = await Assert.ThrowsAsync<HwiException>(async () => await client.SignTxAsync(deviceType, devicePath, Psbt, cts.Token));
			Assert.Equal(HwiErrorCode.BadArgument, ex.ErrorCode);

			// USER: CONFIRM
			var nullFailEx = await Assert.ThrowsAsync<PSBTException>(async () => await client.SignTxAsync(deviceType, devicePath, Psbt, cts.Token));
			Assert.Equal(nullFailEx.Message.Contains("NullFail"), true);

			// USER: CONFIRM
			PSBT signedPsbt = await Gui.Controls.WalletExplorer.SendTabViewModel.SignPsbtWithoutInputTxsAsync(client, fingerprint, Psbt, cts.Token);

			Transaction signedTx = signedPsbt.GetOriginalTransaction();
			Assert.Equal(Psbt.GetOriginalTransaction().GetHash(), signedTx.GetHash());

			var checkResult = signedTx.Check();
			Assert.Equal(TransactionCheckResult.Success, checkResult);
		}
	}
}
