using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi.Trezor;
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

	private static KeyManager WatchOnlyHardwareWallet(bool withCoinJoinAccount)
	{
		var seed = KeyManager.CreateNew(out _, password: "", Network.Main);
		var fingerprint = seed.MasterFingerprint!.Value;
		var coinJoinAccountKeyPath = TrezorDevice.GetCoinJoinAccountKeyPath(Network.Main);

		return withCoinJoinAccount
			? KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, seed.SegwitExtPubKey, seed.TaprootExtPubKey, null, null, Network.Main, null, coinJoinAccountKeyPath)
			: KeyManager.CreateNewHardwareWalletWatchOnly(fingerprint, seed.SegwitExtPubKey, null, null, null, Network.Main);
	}

	[Fact]
	public void OnlyAWalletWithACoinJoinAccountHasARemoteSigner()
	{
		Assert.False(HardwareWalletService.IsRemoteSigner(SoftwareWallet()));
		Assert.False(HardwareWalletService.IsRemoteSigner(WatchOnlyHardwareWallet(withCoinJoinAccount: false)));
		Assert.True(HardwareWalletService.IsRemoteSigner(WatchOnlyHardwareWallet(withCoinJoinAccount: true)));
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
	public async Task ShowingAnAddressOfASoftwareWalletIsRefusedAsync()
	{
		using var service = new HardwareWalletService(Network.Main);
		var keyManager = SoftwareWallet();
		var address = keyManager.GetNextReceiveKey(new LabelsArray("test")).GetAddress(Network.Main);

		var exception = await Assert.ThrowsAsync<HardwareWalletException>(
			() => service.DisplayAddressAsync(keyManager, keyManager.SegwitAccountKeyPath, address, CancellationToken.None));

		Assert.Contains("not on a device", exception.Message);
	}

	[Fact]
	public async Task AuthorizingCoinJoinNeedsAWalletWhoseRoundsADeviceSignsAsync()
	{
		using var service = new HardwareWalletService(Network.Main);
		var keyManager = WatchOnlyHardwareWallet(withCoinJoinAccount: false);

		await Assert.ThrowsAsync<NotSupportedException>(
			() => service.AuthorizeCoinJoinAsync(keyManager, existingKeyChain: null, "coordinator", maxRounds: 1, new FeeRate(1m), CancellationToken.None));
	}

	[Fact]
	public async Task EnablingCoinJoinOnASoftwareWalletIsRefusedAsync()
	{
		using var service = new HardwareWalletService(Network.Main);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => service.EnableCoinJoinAsync(SoftwareWallet(), CancellationToken.None));

		Assert.Contains("hardware wallet", exception.Message);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(500)]
	public void RoundBudgetsTheDeviceCanApproveArePermitted(int rounds) =>
		HardwareWalletService.AssertAuthorizationLimits(rounds, maxMiningFeeRate: null);

	[Theory]
	[InlineData(0)]     // authorizes nothing
	[InlineData(-1)]
	[InlineData(501)]   // beyond what the firmware accepts under its own safety checks
	[InlineData(int.MaxValue)]
	public void RoundBudgetsTheDeviceWouldRefuseAreRejected(int rounds) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => HardwareWalletService.AssertAuthorizationLimits(rounds, maxMiningFeeRate: null));

	[Theory]
	[InlineData(0.5)]
	[InlineData(5)]
	[InlineData(10_000)]
	public void FeeCapsWithinReachArePermitted(decimal feeRate) =>
		HardwareWalletService.AssertAuthorizationLimits(maxRounds: null, feeRate);

	[Theory]
	[InlineData(0)]        // a cap no round could ever meet
	[InlineData(-1)]
	[InlineData(10_001)]   // so far above any fee market that it caps nothing
	public void FeeCapsThatAreNotCapsAreRejected(decimal feeRate) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => HardwareWalletService.AssertAuthorizationLimits(maxRounds: null, feeRate));

	[Fact]
	public void LimitsLeftOutAreNotChecked()
	{
		// Both are optional: a caller that only changes one must not have the other validated against nothing.
		HardwareWalletService.AssertAuthorizationLimits(maxRounds: null, maxMiningFeeRate: null);
	}

	[Fact]
	public void ANewCoinJoinWalletStartsWithinTheLimitsItCanBeAuthorizedWith()
	{
		var keyManager = WatchOnlyHardwareWallet(withCoinJoinAccount: true);

		HardwareWalletService.AssertAuthorizationLimits(keyManager.CoinJoinDeviceMaxRounds, keyManager.CoinJoinDeviceMaxMiningFeeRate);
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
	public void ASignerThatChangedTheAmountIsRejected()
	{
		var built = UnsignedTransfer(Network.Main, Money.Coins(1m), SomeDestination(1), SomeOutPoint(9));
		var tampered = UnsignedTransfer(Network.Main, Money.Coins(2m), SomeDestination(1), SomeOutPoint(9));

		Assert.Throws<HardwareWalletException>(() => HardwareWalletService.AssertSpendsWhatWasBuilt(built, tampered));
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
	/// signing is instant. That has to keep holding for wallets whose keys we hold: only a signer that needs a
	/// real part of the phase may skip the spread, or the schedule stops hiding anything for everyone else.
	/// </summary>
	[Fact]
	public void SoftwareWalletsKeepTheirRandomizedSigningSchedule()
	{
		var keyManager = KeyManager.CreateNew(out _, password: "", Network.Main);
		IKeyChain keyChain = new KeyChain(keyManager, "");

		Assert.False(keyChain.SigningTakesTime);
	}

	[Fact]
	public void ADeviceSignerIsAskedWithoutWaiting()
	{
		// The device is asked as soon as the phase opens, because it spends that phase producing the signature.
		using var transport = new TrezorBridgeTransport("http://127.0.0.1:21325");
		using var device = new TrezorDevice(transport);
		using var keyChain = new TrezorKeyChain(device, WatchOnlyHardwareWallet(withCoinJoinAccount: true));

		Assert.True(((IKeyChain)keyChain).SigningTakesTime);
	}

	[Fact]
	public void NoTransportIsInUseBeforeAnyDeviceOperation()
	{
		using var service = new HardwareWalletService(Network.Main);
		Assert.Equal(HardwareWalletTransport.DirectUsb, service.TransportStatus);
	}

	[Fact]
	public void SigningTimeoutGrowsWithTheNumberOfInputs()
	{
		// A person confirms every output on the device, so more inputs must buy more time - but a small
		// transaction still gets the full base allowance.
		Assert.Equal(TimeSpan.FromMinutes(3), HardwareWalletService.SigningTimeout(0));
		Assert.Equal(TimeSpan.FromMinutes(3), HardwareWalletService.SigningTimeout(9));
		Assert.Equal(TimeSpan.FromMinutes(4), HardwareWalletService.SigningTimeout(10));
		Assert.Equal(TimeSpan.FromMinutes(13), HardwareWalletService.SigningTimeout(100));
	}

	[Fact]
	public void RejectedLimitsExplainThemselves()
	{
		// The settings fields show these strings as they are, so an empty or vague reason would reach the user.
		Assert.False(HardwareWalletService.TryValidateMaxRounds(0, out var roundsError));
		Assert.Contains("between", roundsError);

		Assert.False(HardwareWalletService.TryValidateMaxMiningFeeRate(0m, out var feeRateError));
		Assert.Contains("sat/vByte", feeRateError);

		Assert.True(HardwareWalletService.TryValidateMaxRounds(10, out _));
		Assert.True(HardwareWalletService.TryValidateMaxMiningFeeRate(150m, out _));
	}
}
