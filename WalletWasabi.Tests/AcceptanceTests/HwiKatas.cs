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
		public PSBT Psbt => PSBT.Parse("cHNidP8BAP1dAwEAAAAU1XeL3fOeDJFbX7o7OApTV4FsPFsQX2q7wyw6s//iXO8EAAAAAP/////6819O549nVe/HRhCbWnfVle0gHNQ1ZbB3P9BcmzbsdAEAAAAA/////9V3i93zngyRW1+6OzgKU1eBbDxbEF9qu8MsOrP/4lzvAgAAAAD/////+vNfTuePZ1Xvx0YQm1p31ZXtIBzUNWWwdz/QXJs27HQAAAAAAP/////6819O549nVe/HRhCbWnfVle0gHNQ1ZbB3P9BcmzbsdAMAAAAA//////rzX07nj2dV78dGEJtad9WV7SAc1DVlsHc/0FybNux0AgAAAAD/////+vNfTuePZ1Xvx0YQm1p31ZXtIBzUNWWwdz/QXJs27HQHAAAAAP/////Vd4vd854MkVtfujs4ClNXgWw8WxBfarvDLDqz/+Jc7wAAAAAA//////rzX07nj2dV78dGEJtad9WV7SAc1DVlsHc/0FybNux0BgAAAAD/////+vNfTuePZ1Xvx0YQm1p31ZXtIBzUNWWwdz/QXJs27HQJAAAAAP/////6819O549nVe/HRhCbWnfVle0gHNQ1ZbB3P9BcmzbsdAgAAAAA//////rzX07nj2dV78dGEJtad9WV7SAc1DVlsHc/0FybNux0BAAAAAD/////1XeL3fOeDJFbX7o7OApTV4FsPFsQX2q7wyw6s//iXO8HAAAAAP////+gCBw0gPAWtPJfzpw/qWKkmQIoId/KBH/kQoZIgQysRQAAAAAA/////3VAOsyqkR4ynk6XUNjJbhOq0Ch22BCEumx0kCyxICyIAAAAAAD/////1XeL3fOeDJFbX7o7OApTV4FsPFsQX2q7wyw6s//iXO8GAAAAAP/////Vd4vd854MkVtfujs4ClNXgWw8WxBfarvDLDqz/+Jc7wEAAAAA//////rzX07nj2dV78dGEJtad9WV7SAc1DVlsHc/0FybNux0BQAAAAD/////1XeL3fOeDJFbX7o7OApTV4FsPFsQX2q7wyw6s//iXO8DAAAAAP/////Vd4vd854MkVtfujs4ClNXgWw8WxBfarvDLDqz/+Jc7wUAAAAA/////wGmSAAAAAAAABYAFA+M1lYp2BlxL5LqQhZDXxEOXHN5AAAAAAABAP2IAQIAAAAB+vNfTuePZ1Xvx0YQm1p31ZXt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", Network.Main);

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
