using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi
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
			var client = new HwiClient(network, GetMockedHwiProcessBridge(HardwareWalletModels.Trezor_T));

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.Equal(HardwareWalletModels.Trezor_T, entry.Model);
			Assert.Equal("webusb: 001:4", entry.Path);
			Assert.False(entry.NeedsPassphraseSent);
			Assert.False(entry.NeedsPinSent);
			Assert.NotNull(entry.Error);
			Assert.NotEmpty(entry.Error);
			Assert.Equal(HwiErrorCode.DeviceNotInitialized, entry.Code);
			Assert.False(entry.IsInitialized());
			Assert.Null(entry.Fingerprint);

			var deviceType = entry.Model;
			var devicePath = entry.Path;

			await client.WipeAsync(deviceType, devicePath, cts.Token);
			await client.SetupAsync(deviceType, devicePath, false, cts.Token);
			await client.RestoreAsync(deviceType, devicePath, false, cts.Token);

			// Trezor T doesn't support it.
			var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The PIN has already been sent to this device", promptpin.Message);
			Assert.Equal(HwiErrorCode.DeviceAlreadyUnlocked, promptpin.ErrorCode);

			var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			Assert.Equal("The PIN has already been sent to this device", sendpin.Message);
			Assert.Equal(HwiErrorCode.DeviceAlreadyUnlocked, sendpin.ErrorCode);

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			var expecteXpub1 = NBitcoinHelpers.BetterParseExtPubKey("xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M");
			var expecteXpub2 = NBitcoinHelpers.BetterParseExtPubKey("xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu");
			Assert.Equal(expecteXpub1, xpub1);
			Assert.Equal(expecteXpub2, xpub2);

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);

			BitcoinAddress expectedAddress1;
			BitcoinAddress expectedAddress2;
			if (network == Network.Main)
			{
				expectedAddress1 = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
				expectedAddress2 = BitcoinAddress.Create("bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf", Network.Main);
			}
			else if (network == Network.TestNet)
			{
				expectedAddress1 = BitcoinAddress.Create("tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy", Network.TestNet);
				expectedAddress2 = BitcoinAddress.Create("tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6", Network.TestNet);
			}
			else if (network == Network.RegTest)
			{
				expectedAddress1 = BitcoinAddress.Create("bcrt1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7pzmj3d", Network.RegTest);
				expectedAddress2 = BitcoinAddress.Create("bcrt1qmaveee425a5xjkjcv7m6d4gth45jvtnjz7tugn", Network.RegTest);
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task TrezorOneMockTestsAsync(Network network)
		{
			var client = new HwiClient(network, GetMockedHwiProcessBridge(HardwareWalletModels.Trezor_1));

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.Equal(HardwareWalletModels.Trezor_1, entry.Model);
			string rawPath = "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			string normalizedPath = HwiParser.NormalizeRawDevicePath(rawPath);
			Assert.Equal(normalizedPath, entry.Path);
			Assert.False(entry.NeedsPassphraseSent);
			Assert.True(entry.NeedsPinSent);
			Assert.Equal("Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.", entry.Error);
			Assert.Equal(HwiErrorCode.DeviceNotReady, entry.Code);
			Assert.Null(entry.Fingerprint);
			Assert.True(entry.IsInitialized());

			var deviceType = entry.Model;
			var devicePath = entry.Path;

			await client.WipeAsync(deviceType, devicePath, cts.Token);
			await client.SetupAsync(deviceType, devicePath, false, cts.Token);
			await client.RestoreAsync(deviceType, devicePath, false, cts.Token);

			var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The PIN has already been sent to this device", promptpin.Message);
			Assert.Equal(HwiErrorCode.DeviceAlreadyUnlocked, promptpin.ErrorCode);

			var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			Assert.Equal("The PIN has already been sent to this device", sendpin.Message);
			Assert.Equal(HwiErrorCode.DeviceAlreadyUnlocked, sendpin.ErrorCode);

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			var expecteXpub1 = NBitcoinHelpers.BetterParseExtPubKey("xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M");
			var expecteXpub2 = NBitcoinHelpers.BetterParseExtPubKey("xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu");
			Assert.Equal(expecteXpub1, xpub1);
			Assert.Equal(expecteXpub2, xpub2);

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);

			BitcoinAddress expectedAddress1;
			BitcoinAddress expectedAddress2;
			if (network == Network.Main)
			{
				expectedAddress1 = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
				expectedAddress2 = BitcoinAddress.Create("bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf", Network.Main);
			}
			else if (network == Network.TestNet)
			{
				expectedAddress1 = BitcoinAddress.Create("tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy", Network.TestNet);
				expectedAddress2 = BitcoinAddress.Create("tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6", Network.TestNet);
			}
			else if (network == Network.RegTest)
			{
				expectedAddress1 = BitcoinAddress.Create("bcrt1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7pzmj3d", Network.RegTest);
				expectedAddress2 = BitcoinAddress.Create("bcrt1qmaveee425a5xjkjcv7m6d4gth45jvtnjz7tugn", Network.RegTest);
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task ColdCardMk1MockTestsAsync(Network network)
		{
			var client = new HwiClient(network, GetMockedHwiProcessBridge(HardwareWalletModels.Coldcard));

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.Equal(HardwareWalletModels.Coldcard, entry.Model);
			string rawPath = "\\\\\\\\?\\\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			string normalizedPath = HwiParser.NormalizeRawDevicePath(rawPath);
			Assert.Equal(normalizedPath, entry.Path);
			Assert.Null(entry.NeedsPassphraseSent);
			Assert.Null(entry.NeedsPinSent);
			Assert.Null(entry.Error);
			Assert.Null(entry.Code);
			Assert.Equal("a3d0d797", entry.Fingerprint.ToString());
			Assert.True(entry.IsInitialized());

			var deviceType = entry.Model;
			var devicePath = entry.Path;
			HDFingerprint fingerprint = entry.Fingerprint.Value;

			var wipe = await Assert.ThrowsAsync<HwiException>(async () => await client.WipeAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Coldcard does not support wiping via software", wipe.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, wipe.ErrorCode);

			// ColdCard doesn't support it.
			var setup = await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Coldcard does not support software setup", setup.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, setup.ErrorCode);

			// ColdCard doesn't support it.
			var restore = await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Coldcard does not support restoring via software", restore.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, restore.ErrorCode);

			// ColdCard doesn't support it.
			var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Coldcard does not need a PIN sent from the host", promptpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, promptpin.ErrorCode);

			// ColdCard doesn't support it.
			var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			Assert.Equal("The Coldcard does not need a PIN sent from the host", sendpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, sendpin.ErrorCode);

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			var expecteXpub1 = NBitcoinHelpers.BetterParseExtPubKey("xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M");
			var expecteXpub2 = NBitcoinHelpers.BetterParseExtPubKey("xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu");
			Assert.Equal(expecteXpub1, xpub1);
			Assert.Equal(expecteXpub2, xpub2);

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);

			BitcoinAddress expectedAddress1;
			BitcoinAddress expectedAddress2;
			if (network == Network.Main)
			{
				expectedAddress1 = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
				expectedAddress2 = BitcoinAddress.Create("bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf", Network.Main);
			}
			else if (network == Network.TestNet)
			{
				expectedAddress1 = BitcoinAddress.Create("tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy", Network.TestNet);
				expectedAddress2 = BitcoinAddress.Create("tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6", Network.TestNet);
			}
			else if (network == Network.RegTest)
			{
				expectedAddress1 = BitcoinAddress.Create("bcrt1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7pzmj3d", Network.RegTest);
				expectedAddress2 = BitcoinAddress.Create("bcrt1qmaveee425a5xjkjcv7m6d4gth45jvtnjz7tugn", Network.RegTest);
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task LedgerNanoSTestsAsync(Network network)
		{
			var client = new HwiClient(network, GetMockedHwiProcessBridge(HardwareWalletModels.Ledger_Nano_S));

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.Equal(HardwareWalletModels.Ledger_Nano_S, entry.Model);
			Assert.Equal(@"\\?\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", entry.Path);
			Assert.False(entry.NeedsPassphraseSent);
			Assert.False(entry.NeedsPinSent);
			Assert.Null(entry.Error);
			Assert.Null(entry.Code);
			Assert.True(entry.IsInitialized());
			Assert.Equal("4054d6f6", entry.Fingerprint.ToString());

			var deviceType = entry.Model;
			var devicePath = entry.Path;

			var wipe = await Assert.ThrowsAsync<HwiException>(async () => await client.WipeAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Ledger Nano S does not support wiping via software", wipe.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, wipe.ErrorCode);

			var setup = await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Ledger Nano S does not support software setup", setup.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, setup.ErrorCode);

			var restore = await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Ledger Nano S does not support restoring via software", restore.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, restore.ErrorCode);

			var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Ledger Nano S does not need a PIN sent from the host", promptpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, promptpin.ErrorCode);

			var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			Assert.Equal("The Ledger Nano S does not need a PIN sent from the host", sendpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, sendpin.ErrorCode);

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			var expecteXpub1 = NBitcoinHelpers.BetterParseExtPubKey("xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M");
			var expecteXpub2 = NBitcoinHelpers.BetterParseExtPubKey("xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu");
			Assert.Equal(expecteXpub1, xpub1);
			Assert.Equal(expecteXpub2, xpub2);

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);

			BitcoinAddress expectedAddress1;
			BitcoinAddress expectedAddress2;
			if (network == Network.Main)
			{
				expectedAddress1 = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
				expectedAddress2 = BitcoinAddress.Create("bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf", Network.Main);
			}
			else if (network == Network.TestNet)
			{
				expectedAddress1 = BitcoinAddress.Create("tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy", Network.TestNet);
				expectedAddress2 = BitcoinAddress.Create("tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6", Network.TestNet);
			}
			else if (network == Network.RegTest)
			{
				expectedAddress1 = BitcoinAddress.Create("bcrt1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7pzmj3d", Network.RegTest);
				expectedAddress2 = BitcoinAddress.Create("bcrt1qmaveee425a5xjkjcv7m6d4gth45jvtnjz7tugn", Network.RegTest);
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task LedgerNanoXTestsAsync(Network network)
		{
			var client = new HwiClient(network, GetMockedHwiProcessBridge(HardwareWalletModels.Ledger_Nano_X));

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
			Assert.Single(enumerate);
			HwiEnumerateEntry entry = enumerate.Single();
			Assert.Equal(HardwareWalletModels.Ledger_Nano_X, entry.Model);
			Assert.Equal(@"\\?\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", entry.Path);
			Assert.False(entry.NeedsPassphraseSent);
			Assert.False(entry.NeedsPinSent);
			Assert.Null(entry.Error);
			Assert.Null(entry.Code);
			Assert.True(entry.IsInitialized());
			Assert.Equal("4054d6f6", entry.Fingerprint.ToString());

			var deviceType = entry.Model;
			var devicePath = entry.Path;

			var wipe = await Assert.ThrowsAsync<HwiException>(async () => await client.WipeAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Ledger Nano X does not support wiping via software", wipe.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, wipe.ErrorCode);

			var setup = await Assert.ThrowsAsync<HwiException>(async () => await client.SetupAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Ledger Nano X does not support software setup", setup.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, setup.ErrorCode);

			var restore = await Assert.ThrowsAsync<HwiException>(async () => await client.RestoreAsync(deviceType, devicePath, false, cts.Token));
			Assert.Equal("The Ledger Nano X does not support restoring via software", restore.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, restore.ErrorCode);

			var promptpin = await Assert.ThrowsAsync<HwiException>(async () => await client.PromptPinAsync(deviceType, devicePath, cts.Token));
			Assert.Equal("The Ledger Nano X does not need a PIN sent from the host", promptpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, promptpin.ErrorCode);

			var sendpin = await Assert.ThrowsAsync<HwiException>(async () => await client.SendPinAsync(deviceType, devicePath, 1111, cts.Token));
			Assert.Equal("The Ledger Nano X does not need a PIN sent from the host", sendpin.Message);
			Assert.Equal(HwiErrorCode.UnavailableAction, sendpin.ErrorCode);

			KeyPath keyPath1 = KeyManager.DefaultAccountKeyPath;
			KeyPath keyPath2 = KeyManager.DefaultAccountKeyPath.Derive(1);
			ExtPubKey xpub1 = await client.GetXpubAsync(deviceType, devicePath, keyPath1, cts.Token);
			ExtPubKey xpub2 = await client.GetXpubAsync(deviceType, devicePath, keyPath2, cts.Token);
			var expecteXpub1 = NBitcoinHelpers.BetterParseExtPubKey("xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M");
			var expecteXpub2 = NBitcoinHelpers.BetterParseExtPubKey("xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu");
			Assert.Equal(expecteXpub1, xpub1);
			Assert.Equal(expecteXpub2, xpub2);

			BitcoinWitPubKeyAddress address1 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath1, cts.Token);
			BitcoinWitPubKeyAddress address2 = await client.DisplayAddressAsync(deviceType, devicePath, keyPath2, cts.Token);

			BitcoinAddress expectedAddress1;
			BitcoinAddress expectedAddress2;
			if (network == Network.Main)
			{
				expectedAddress1 = BitcoinAddress.Create("bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah", Network.Main);
				expectedAddress2 = BitcoinAddress.Create("bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf", Network.Main);
			}
			else if (network == Network.TestNet)
			{
				expectedAddress1 = BitcoinAddress.Create("tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy", Network.TestNet);
				expectedAddress2 = BitcoinAddress.Create("tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6", Network.TestNet);
			}
			else if (network == Network.RegTest)
			{
				expectedAddress1 = BitcoinAddress.Create("bcrt1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7pzmj3d", Network.RegTest);
				expectedAddress2 = BitcoinAddress.Create("bcrt1qmaveee425a5xjkjcv7m6d4gth45jvtnjz7tugn", Network.RegTest);
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}

			Assert.Equal(expectedAddress1, address1);
			Assert.Equal(expectedAddress2, address2);
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

		public IHwiProcessInvoker GetMockedHwiProcessBridge(HardwareWalletModels model)
		{
			var mockedBridge = new Mock<IHwiProcessInvoker>();

			mockedBridge.Setup(x => x.SendCommandAsync(
				It.IsAny<string>(),
				It.IsAny<bool>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<Action<StreamWriter>?>()))
				.Returns((string arguments, bool openConsole, CancellationToken cancel, Action<StreamWriter>? writer)
				=>
				SendCommandAsyncMock(arguments, openConsole, model, cancel, writer))
				;

			return mockedBridge.Object;
		}

		private Task<(string response, int exitCode)> SendCommandAsyncMock(string arguments, bool openConsole, HardwareWalletModels model, CancellationToken cancel, Action<StreamWriter>? standardInputWriter = null)
		{
			if (openConsole)
			{
				throw new NotImplementedException($"Cannot mock {nameof(openConsole)} mode.");
			}

			string modelAsString;
			string rawPath;

			if (model == HardwareWalletModels.Trezor_T)
			{
				modelAsString = "trezor_t";
				rawPath = "webusb: 001:4";
			}
			else if (model == HardwareWalletModels.Trezor_1)
			{
				modelAsString = "trezor_1";
				rawPath = "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else if (model == HardwareWalletModels.Coldcard)
			{
				modelAsString = "coldcard";
				rawPath = @"\\\\?\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else if (model == HardwareWalletModels.Ledger_Nano_S)
			{
				modelAsString = "ledger_nano_s";
				rawPath = "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else if (model == HardwareWalletModels.Ledger_Nano_X)
			{
				modelAsString = "ledger_nano_x";
				rawPath = "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else
			{
				throw new NotImplementedException("Mock missing.");
			}

			string path = HwiParser.NormalizeRawDevicePath(rawPath);
			string devicePathAndTypeArgumentString = $"--device-path \"{path}\" --device-type \"{modelAsString}\"";

			const string SuccessTrueResponse = "{\"success\": true}\r\n";

			string? response = null;
			int code = 0;

			if (CompareArguments(arguments, "enumerate"))
			{
				if (model == HardwareWalletModels.Trezor_T)
				{
					response = $"[{{\"model\": \"{modelAsString}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}}]";
				}
				else if (model == HardwareWalletModels.Trezor_1)
				{
					response = $"[{{\"model\": \"{modelAsString}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": true, \"needs_passphrase_sent\": false, \"error\": \"Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.\", \"code\": -12}}]\r\n";
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = $"[{{\"model\": \"{modelAsString}\", \"path\": \"{rawPath}\", \"needs_passphrase\": false, \"fingerprint\": \"a3d0d797\"}}]\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = $"[{{\"model\": \"{modelAsString}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = $"[{{\"model\": \"{modelAsString}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} wipe"))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = SuccessTrueResponse;
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not support wiping via software\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not support wiping via software\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not support wiping via software\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} setup"))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = "{\"error\": \"setup requires interactive mode\", \"code\": -9}";
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive setup"))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = SuccessTrueResponse;
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive restore"))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = SuccessTrueResponse;
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not support restoring via software\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not support restoring via software\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not support restoring via software\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} promptpin"))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}\r\n";
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} sendpin", true))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1)
				{
					response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}";
				}
				else if (model == HardwareWalletModels.Coldcard)
				{
					response = "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_S)
				{
					response = "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (model == HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
			}
			else if (CompareGetXbpubArguments(arguments, out string? xpub))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = $"{{\"xpub\": \"{xpub}\"}}\r\n";
				}
			}
			else if (CompareArgumentsMock(out bool t1, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h --addr-type wit", false))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = t1
						? "{\"address\": \"tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy\"}\r\n"
						: "{\"address\": \"bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah\"}\r\n";
				}
			}
			else if (CompareArgumentsMock(out bool t2, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h/1 --addr-type wit", false))
			{
				if (model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = t2
						? "{\"address\": \"tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6\"}\r\n"
						: "{\"address\": \"bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf\"}\r\n";
				}
			}

			return response is null
				? throw new NotImplementedException($"Mocking is not implemented for '{arguments}'.")
				: Task.FromResult((response, code));
		}

		private bool CompareArgumentsMock(out bool isTestNet, string arguments, string desired, bool useStartWith = false)
		{
			var testnetDesired = $"--chain test {desired}";
			isTestNet = false;

			if (useStartWith)
			{
				if (arguments.StartsWith(desired, StringComparison.Ordinal))
				{
					return true;
				}

				if (arguments.StartsWith(testnetDesired, StringComparison.Ordinal))
				{
					isTestNet = true;
					return true;
				}
			}
			else
			{
				if (arguments == desired)
				{
					return true;
				}

				if (arguments == testnetDesired)
				{
					isTestNet = true;
					return true;
				}
			}

			return false;
		}

		private bool CompareArguments(string arguments, string desired, bool useStartWith = false)
			=> CompareArgumentsMock(out _, arguments, desired, useStartWith);

		private bool CompareGetXbpubArguments(string arguments, [NotNullWhen(returnValue: true)] out string? extPubKey)
		{
			extPubKey = null;
			string command = "getxpub";
			if (arguments.Contains(command, StringComparison.Ordinal)
				&& ((arguments.Contains("--device-path", StringComparison.Ordinal) && arguments.Contains("--device-type", StringComparison.Ordinal))
					|| arguments.Contains("--fingerprint")))
			{
				// The +1 is the space.
				var keyPath = arguments[(arguments.IndexOf(command) + command.Length + 1)..];
				if (keyPath == "m/84h/0h/0h")
				{
					extPubKey = "xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M";
				}
				else if (keyPath == "m/84h/0h/0h/1")
				{
					extPubKey = "xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu";
				}
			}

			return extPubKey is { };
		}

		#endregion HelperMethods
	}
}
