using System.Globalization;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Hwi;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

/// <summary>
/// The service refuses device operations that cannot apply to a wallet, before any device is touched.
/// These are the checks that keep a caller from getting a transport error where the real answer is that
/// the wallet has no device at all.
/// </summary>
public class HardwareWalletServiceTests
{
	private static KeyManager SoftwareWallet() =>
		KeyManager.CreateNew(out _, password: "", Network.Main);

	[Fact]
	public void OnlyAWalletWithACoinJoinAccountHasARemoteSigner()
	{
		Assert.False(HardwareWalletService.IsRemoteSigner(SoftwareWallet()));
		Assert.False(HardwareWalletService.IsRemoteSigner(TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: false)));
		Assert.True(HardwareWalletService.IsRemoteSigner(TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: true)));
	}

	[Fact]
	public async Task SigningASoftwareWalletIsRefusedBeforeTouchingADeviceAsync()
	{
		using var service = new HardwareWalletService(Network.Main);
		var keyManager = SoftwareWallet();
		var psbt = PSBT.Parse("cHNidP8BAAoAAAAAAAAAAAAAAA==", Network.Main);

		var exception = await Assert.ThrowsAsync<HardwareWalletException>(
			() => service.SignTransactionAsync(keyManager, psbt, transaction: null!, CancellationToken.None));

		Assert.Contains("not on a device", exception.Message);
	}

	[Fact]
	public async Task AuthorizingCoinJoinNeedsAWalletWhoseRoundsADeviceSignsAsync()
	{
		using var service = new HardwareWalletService(Network.Main);
		var keyManager = TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: false);

		await Assert.ThrowsAsync<NotSupportedException>(
			() => service.AuthorizeCoinJoinAsync(keyManager, existingKeyChain: null, "coordinator", maxRounds: 1, new FeeRate(1m), CancellationToken.None));
	}

	[Fact]
	public async Task EnablingCoinJoinOnASoftwareWalletIsRefusedAsync()
	{
		using var service = new HardwareWalletService(Network.Main);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.EnableCoinJoinAsync(SoftwareWallet(), addressToConfirm: null, CancellationToken.None));

		Assert.Contains("hardware wallet", exception.Message);
	}

	/// <summary>
	/// The settings fields show the reason as it is, so a rejected limit must name the bound it missed. A limit
	/// left out is not checked: a caller that only changes one must not have the other validated against nothing.
	/// </summary>
	[Theory]
	[InlineData(null, true)]
	[InlineData(0, false)]    // authorizes nothing
	[InlineData(1, true)]
	[InlineData(500, true)]
	[InlineData(501, false)]  // beyond what the firmware accepts under its own safety checks
	public void RoundBudgetsAreBoundedByWhatTheDeviceApproves(int? rounds, bool permitted)
	{
		Assert.Equal(permitted, HardwareWalletService.TryValidateMaxRounds(rounds, out var error));

		if (permitted)
		{
			Assert.Null(error);
		}
		else
		{
			Assert.Contains("between 1 and 500", error);
		}
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData("0", false)]        // a cap no round could ever meet
	[InlineData("0.5", true)]
	[InlineData("10000", true)]
	[InlineData("10001", false)]   // so far above any fee market that it caps nothing
	public void FeeCapsAreBoundedByWhatStillCapsSomething(string? feeRate, bool permitted)
	{
		// Strings, because xunit cannot convert a literal into a nullable decimal.
		decimal? cap = feeRate is null ? null : decimal.Parse(feeRate, CultureInfo.InvariantCulture);
		Assert.Equal(permitted, HardwareWalletService.TryValidateMaxMiningFeeRate(cap, out var error));

		if (permitted)
		{
			Assert.Null(error);
		}
		else
		{
			Assert.Contains("above 0 and at most 10000 sat/vByte", error);
		}
	}

	[Fact]
	public void EitherLimitOutOfRangeStopsAnAuthorization()
	{
		HardwareWalletService.AssertAuthorizationLimits(KeyManager.DefaultCoinJoinDeviceMaxRounds, KeyManager.DefaultCoinJoinDeviceMaxMiningFeeRate);

		Assert.Throws<ArgumentOutOfRangeException>(() => HardwareWalletService.AssertAuthorizationLimits(0, 150m));
		Assert.Throws<ArgumentOutOfRangeException>(() => HardwareWalletService.AssertAuthorizationLimits(10, 0m));
	}

	/// <summary>
	/// A signer runs in another process and the user only approved what their device showed, so a signed
	/// transaction that spends other coins or pays other outputs must never reach the network.
	/// </summary>
	private static PSBT UnsignedTransfer(Network network, Money amount, Script destination, params OutPoint[] inputs)
	{
		var tx = network.CreateTransaction();
		foreach (var input in inputs)
		{
			tx.Inputs.Add(new TxIn(input));
		}
		tx.Outputs.Add(new TxOut(amount, destination));
		return PSBT.FromTransaction(tx, network);
	}

	private static OutPoint SomeOutPoint(byte seed) => new(new uint256(Enumerable.Repeat(seed, 32).ToArray()), 0);

	private static Script SomeDestination(byte seed)
	{
		using var key = new Key(Enumerable.Repeat(seed, 32).ToArray());
		return key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
	}

	[Fact]
	public void ASignerThatChangedTheDestinationIsRejected()
	{
		var built = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(9));
		var tampered = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(2), SomeOutPoint(9));

		var exception = Assert.Throws<HardwareWalletException>(() => HardwareWalletService.AssertSpendsWhatWasBuilt(built, tampered));
		Assert.Contains("not broadcast", exception.Message);
	}

	[Fact]
	public void ASignerThatChangedWhichCoinsAreSpentIsRejected()
	{
		var built = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(9));
		var tampered = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(8));

		Assert.Throws<HardwareWalletException>(() => HardwareWalletService.AssertSpendsWhatWasBuilt(built, tampered));
	}

	[Fact]
	public void ASignerThatAddedAnOutputIsRejected()
	{
		var built = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(9));
		var tampered = built.Clone();
		var tamperedTx = tampered.GetGlobalTransaction();
		tamperedTx.Outputs.Add(new TxOut(Money.Coins(1m), SomeDestination(3)));

		Assert.Throws<HardwareWalletException>(() => HardwareWalletService.AssertSpendsWhatWasBuilt(built, PSBT.FromTransaction(tamperedTx, Network.Main)));
	}

	[Fact]
	public void TheTransactionThatWasBuiltIsAccepted()
	{
		var built = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(9), SomeOutPoint(8));
		var signed = built.Clone();

		HardwareWalletService.AssertSpendsWhatWasBuilt(built, signed);
	}

	/// <summary>
	/// Signing requests are spread over the signing phase to hide timing from the coordinator, which assumes
	/// signing is instant. Only a signer that spends a real part of the phase producing the signature may skip
	/// the spread, or the schedule stops hiding anything for everyone else.
	/// </summary>
	[Fact]
	public void OnlyADeviceSignerSkipsTheRandomizedSigningSchedule()
	{
		using var transport = new ScriptedTransport();
		using var device = new TrezorDevice(transport);
		using var deviceKeyChain = new TrezorKeyChain(device, TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: true));
		IKeyChain softwareKeyChain = new KeyChain(SoftwareWallet(), "");

		Assert.True(((IKeyChain)deviceKeyChain).SigningTakesTime);
		Assert.False(softwareKeyChain.SigningTakesTime);
	}

	[Fact]
	public void SigningTimeoutGrowsWithTheNumberOfInputs()
	{
		// A person confirms every output on the device, so more inputs must buy more time - but a small
		// transaction still gets the full base allowance.
		Assert.Equal(TimeSpan.FromMinutes(3), HardwareWalletService.SigningTimeout(9));
		Assert.Equal(TimeSpan.FromMinutes(4), HardwareWalletService.SigningTimeout(10));
		Assert.Equal(TimeSpan.FromMinutes(13), HardwareWalletService.SigningTimeout(100));
	}

	private static TrezorMessage PublicKey(ExtPubKey extPubKey) =>
		new(TrezorMessageType.PublicKey, new ProtoWriter().WriteStringField(2, extPubKey.ToString(Network.Main)).ToBytes());

	private static TrezorMessage Address(BitcoinAddress address) =>
		new(TrezorMessageType.Address, new ProtoWriter().WriteStringField(1, address.ToString()).ToBytes());

	private static BitcoinAddress SegwitAddress(KeyManager seed, uint index) =>
		seed.SegwitExtPubKey.Derive(0).Derive(index).PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);

	private sealed class ShownAddresses : List<BitcoinAddress>, IProgress<BitcoinAddress>
	{
		public void Report(BitcoinAddress address) => Add(address);
	}

	/// <summary>
	/// The bridge is an unauthenticated local process, so an account it hands out is only saved once the
	/// device has shown the first address of the account and the caller was told which address to expect.
	/// </summary>
	[Fact]
	public async Task AnImportedAccountIsSavedOnlyAfterTheDeviceShowedItsAddressAsync()
	{
		var seed = KeyManager.CreateNew(out _, password: "", Network.Main);
		var walletFilePath = Path.Combine(await Common.GetEmptyWorkDirAsync(), "wallet.json");
		using var service = new HardwareWalletService(Network.Main);
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(PublicKey(seed.SegwitExtPubKey));
		transport.Responses.Enqueue(Address(SegwitAddress(seed, 0)));
		using var device = new TrezorDevice(transport);
		var shown = new ShownAddresses();

		var keyManager = await service.ReadAccountsAsync(device, seed.MasterFingerprint!.Value, walletFilePath, enableCoinjoin: false, shown, CancellationToken.None);

		Assert.Equal(TrezorMessageType.GetAddress, transport.Received.Last().MessageType);
		Assert.Equal(SegwitAddress(seed, 0), Assert.Single(shown));
		Assert.Equal(seed.SegwitExtPubKey, keyManager.SegwitExtPubKey);
		Assert.True(File.Exists(walletFilePath));
	}

	[Fact]
	public async Task AnAccountWhoseAddressTheDeviceDoesNotShowIsNotSavedAsync()
	{
		var seed = KeyManager.CreateNew(out _, password: "", Network.Main);
		var walletFilePath = Path.Combine(await Common.GetEmptyWorkDirAsync(), "wallet.json");
		using var service = new HardwareWalletService(Network.Main);
		using var transport = new ScriptedTransport();
		transport.Responses.Enqueue(PublicKey(seed.SegwitExtPubKey));
		transport.Responses.Enqueue(Address(SegwitAddress(seed, 1)));
		using var device = new TrezorDevice(transport);

		var exception = await Assert.ThrowsAsync<HardwareWalletException>(
			() => service.ReadAccountsAsync(device, seed.MasterFingerprint!.Value, walletFilePath, enableCoinjoin: false, addressToConfirm: null, CancellationToken.None));

		Assert.Contains("different address", exception.Message);
		Assert.False(File.Exists(walletFilePath));
	}
}
