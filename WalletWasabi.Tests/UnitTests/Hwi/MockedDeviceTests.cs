using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.KeyManagement;
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
			var client = new HwiClient(network, new HwiProcessBridgeMock(HardwareWalletModels.Trezor_T));

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletModels.Trezor_T, entry.Type);
				Assert.Equal("webusb: 001:4", entry.Path);
				Assert.False(entry.NeedsPassphraseSent);
				Assert.False(entry.NeedsPinSent);
				Assert.NotNull(entry.Error);
				Assert.NotEmpty(entry.Error);
				Assert.Equal(HwiErrorCode.DeviceNotInitialized, entry.Code);
				Assert.False(entry.IsInitialized());
				Assert.Null(entry.Fingerprint);

				var deviceType = entry.Type;
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
					throw new NotSupportedException($"{network} not supported.");
				}

				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);
			}
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task TrezorOneMockTestsAsync(Network network)
		{
			var client = new HwiClient(network, new HwiProcessBridgeMock(HardwareWalletModels.Trezor_1));

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletModels.Trezor_1, entry.Type);
				string rawPath = "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
				string normalizedPath = HwiParser.NormalizeRawDevicePath(rawPath);
				Assert.Equal(normalizedPath, entry.Path);
				Assert.False(entry.NeedsPassphraseSent);
				Assert.True(entry.NeedsPinSent);
				Assert.Equal("Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.", entry.Error);
				Assert.Equal(HwiErrorCode.DeviceNotReady, entry.Code);
				Assert.Null(entry.Fingerprint);
				Assert.True(entry.IsInitialized());

				var deviceType = entry.Type;
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
					throw new NotSupportedException($"{network} not supported.");
				}

				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);
			}
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task ColdCardMk1MockTestsAsync(Network network)
		{
			var client = new HwiClient(network, new HwiProcessBridgeMock(HardwareWalletModels.Coldcard));

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletModels.Coldcard, entry.Type);
				string rawPath = "\\\\\\\\?\\\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
				string normalizedPath = HwiParser.NormalizeRawDevicePath(rawPath);
				Assert.Equal(normalizedPath, entry.Path);
				Assert.Null(entry.NeedsPassphraseSent);
				Assert.Null(entry.NeedsPinSent);
				Assert.Null(entry.Error);
				Assert.Null(entry.Code);
				Assert.Equal("a3d0d797", entry.Fingerprint.ToString());
				Assert.True(entry.IsInitialized());

				var deviceType = entry.Type;
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
					throw new NotSupportedException($"{network} not supported.");
				}

				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);
			}
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task LedgerNanoSTestsAsync(Network network)
		{
			var client = new HwiClient(network, new HwiProcessBridgeMock(HardwareWalletModels.Ledger_Nano_S));

			using (var cts = new CancellationTokenSource(ReasonableRequestTimeout))
			{
				IEnumerable<HwiEnumerateEntry> enumerate = await client.EnumerateAsync(cts.Token);
				Assert.Single(enumerate);
				HwiEnumerateEntry entry = enumerate.Single();
				Assert.Equal(HardwareWalletModels.Ledger_Nano_S, entry.Type);
				Assert.Equal(@"\\?\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", entry.Path);
				Assert.False(entry.NeedsPassphraseSent);
				Assert.False(entry.NeedsPinSent);
				Assert.Null(entry.Error);
				Assert.Null(entry.Code);
				Assert.True(entry.IsInitialized());
				Assert.Equal("4054d6f6", entry.Fingerprint.ToString());

				var deviceType = entry.Type;
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
					throw new NotSupportedException($"{network} not supported.");
				}

				Assert.Equal(expectedAddress1, address1);
				Assert.Equal(expectedAddress2, address2);
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
