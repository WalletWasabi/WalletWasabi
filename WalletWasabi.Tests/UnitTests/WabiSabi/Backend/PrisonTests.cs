using System.Linq;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

/// <summary>
/// Tests for <see cref="Prison"/>.
/// </summary>
public class PrisonTests
{
	[Fact]
	public async Task OffensesAreSavedAsync()
	{
		using CancellationTokenSource ctsTimeout = new(TimeSpan.FromMinutes(1));

		var (prison, reader) = WabiSabiFactory.CreateObservablePrison();

		var outpoint = BitcoinFactory.CreateOutPoint();
		var roundId = BitcoinFactory.CreateUint256();

		// Fail to verify
		prison.FailedVerification(outpoint, roundId);
		var offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var failedToVerify = Assert.IsType<FailedToVerify>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, failedToVerify.VerifiedInRoundId);

		// Fail to confirm
		prison.FailedToConfirm(outpoint, Money.Coins(1m), roundId);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var disruptionNotConfirming = Assert.IsType<RoundDisruption>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, Assert.Single(disruptionNotConfirming.DisruptedRoundIds));
		Assert.Equal(RoundDisruptionMethod.DidNotConfirm, disruptionNotConfirming.Method);
		Assert.Equal(Money.Coins(1m), disruptionNotConfirming.Value);

		// Fail to signal ready to sign
		prison.FailedToSignalReadyToSign(outpoint, Money.Coins(1m), roundId);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var disruptionNotSignallingReadyToSign = Assert.IsType<RoundDisruption>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, Assert.Single(disruptionNotSignallingReadyToSign.DisruptedRoundIds));
		Assert.Equal(RoundDisruptionMethod.DidNotSignalReadyToSign, disruptionNotSignallingReadyToSign.Method);
		Assert.Equal(Money.Coins(1m), disruptionNotSignallingReadyToSign.Value);

		// Fail to sign
		prison.FailedToSign(outpoint, Money.Coins(2m), roundId);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var disruptionNotSigning = Assert.IsType<RoundDisruption>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, Assert.Single(disruptionNotSigning.DisruptedRoundIds));
		Assert.Equal(RoundDisruptionMethod.DidNotSign, disruptionNotSigning.Method);
		Assert.Equal(Money.Coins(2m), disruptionNotSigning.Value);

		// Double spent
		prison.DoubleSpent(outpoint, Money.Coins(3m), roundId);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var doubleSpending = Assert.IsType<RoundDisruption>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, Assert.Single(doubleSpending.DisruptedRoundIds));
		Assert.Equal(RoundDisruptionMethod.DoubleSpent, doubleSpending.Method);
		Assert.Equal(Money.Coins(3m), doubleSpending.Value);

		// Cheating
		prison.CheatingDetected(outpoint, roundId);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var cheating = Assert.IsType<Cheating>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(roundId, cheating.RoundId);

		// Inherited
		var ancestors = new[]
		{
			BitcoinFactory.CreateOutPoint(),
			BitcoinFactory.CreateOutPoint()
		};

		prison.InheritPunishment(outpoint, ancestors);
		offenderToSave = await reader.ReadAsync(ctsTimeout.Token);
		var inherited = Assert.IsType<Inherited>(offenderToSave.Offense);
		Assert.Equal(outpoint, offenderToSave.OutPoint);
		Assert.Equal(ancestors[0], inherited.Ancestors[0]);
		Assert.Equal(ancestors[1], inherited.Ancestors[1]);

		Assert.Equal(0, reader.Count);
	}

	[Fact]
	public void BanningTime()
	{
		var (prison, _) = WabiSabiFactory.CreateObservablePrison();
		var cfg = WabiSabiFactory.CreateConfig().GetDoSConfiguration();

		var roundId = BitcoinFactory.CreateUint256();

		// Failed to verify punishment is constant (not affected by number of attempts)
		var ftvOutpoint = BitcoinFactory.CreateOutPoint();
		prison.FailedVerification(ftvOutpoint, roundId);
		prison.FailedVerification(ftvOutpoint, roundId);
		prison.FailedVerification(ftvOutpoint, roundId);
		var banningPeriod = prison.GetBanTimePeriod(ftvOutpoint, cfg);
		Assert.Equal(cfg.MinTimeForFailedToVerify, banningPeriod.Duration);

		// Cheating punishment is constant (not affected by number of attempts)
		var chtOutpoint = BitcoinFactory.CreateOutPoint();
		prison.CheatingDetected(chtOutpoint, roundId);
		prison.CheatingDetected(chtOutpoint, roundId);
		prison.CheatingDetected(chtOutpoint, roundId);
		banningPeriod = prison.GetBanTimePeriod(chtOutpoint, cfg);
		Assert.Equal(cfg.MinTimeForCheating, banningPeriod.Duration);

		// Failed to confirm is calculated and inversely proportional to the amount
		var ftcOutpoint1 = BitcoinFactory.CreateOutPoint();
		var ftcOutpoint2 = BitcoinFactory.CreateOutPoint();
		prison.FailedToConfirm(ftcOutpoint1, Money.Coins(0.5m), roundId);
		prison.FailedToConfirm(ftcOutpoint2, Money.Coins(1.0m), roundId);

		var banningPeriod1 = prison.GetBanTimePeriod(ftcOutpoint1, cfg);
		var banningPeriod2 = prison.GetBanTimePeriod(ftcOutpoint2, cfg);
		Assert.True(banningPeriod1.Duration == 2 * banningPeriod2.Duration);

		// .... second attempt is punished harder
		prison.FailedToConfirm(ftcOutpoint1, Money.Coins(0.5m), roundId);
		var banningPeriod1FailedToConfirmTwice = prison.GetBanTimePeriod(ftcOutpoint1, cfg);
		Assert.True(banningPeriod1FailedToConfirmTwice.Duration > banningPeriod1.Duration);

		// .... the worst offense is applied
		// Note: this case is compared against ftcOutpoint1 which failed to confirm twice
		var ftcOutpoint3 = BitcoinFactory.CreateOutPoint();
		prison.FailedToConfirm(ftcOutpoint3, Money.Coins(0.5m), roundId);
		prison.FailedToSign(ftcOutpoint3, Money.Coins(0.5m), roundId);

		var banningPeriod3FailedToConfirmAndSign = prison.GetBanTimePeriod(ftcOutpoint3, cfg);
		Assert.True(banningPeriod3FailedToConfirmAndSign.Duration > banningPeriod1FailedToConfirmTwice.Duration);

		// Big amounts are not banned the first time
		var ftcOutpointBig = BitcoinFactory.CreateOutPoint();
		prison.FailedToConfirm(ftcOutpointBig, Money.Coins(1.3m), roundId);
		var banningPeriodBigCoin = prison.GetBanTimePeriod(ftcOutpointBig, cfg);
		Assert.Equal(TimeSpan.Zero, banningPeriodBigCoin.Duration);

		// ... however, second attempts could be punished
		prison.FailedToConfirm(ftcOutpointBig, Money.Coins(1.3m), roundId);
		banningPeriodBigCoin = prison.GetBanTimePeriod(ftcOutpointBig, cfg);
		Assert.NotEqual(TimeSpan.Zero, banningPeriodBigCoin.Duration);

		// coins inherit the punishments from their ancestors
		var ftcFailedToSign = BitcoinFactory.CreateOutPoint();
		var ftcFailedToConfirm = BitcoinFactory.CreateOutPoint();
		var ftcInheritFromFailedToSign = BitcoinFactory.CreateOutPoint();
		prison.FailedToSign(ftcFailedToSign, Money.Coins(0.005m), roundId);
		prison.FailedToConfirm(ftcFailedToConfirm, Money.Coins(0.005m), roundId);
		prison.InheritPunishment(ftcInheritFromFailedToSign, new[] { ftcFailedToSign, ftcFailedToConfirm });
		var failToSignBanningTime = prison.GetBanTimePeriod(ftcFailedToSign, cfg);
		var inheritedBanningTime = prison.GetBanTimePeriod(ftcInheritFromFailedToSign, cfg);

		Assert.Equal(failToSignBanningTime.StartTime, inheritedBanningTime.StartTime); // because fail to sign is punished harder
		Assert.Equal(failToSignBanningTime.Duration * 0.5, inheritedBanningTime.Duration); // after spending the punishment is reduced by half

		// After spending a couple of times the coin is no longer banned
		var outpointsToBan = Enumerable.Range(0, 10).Select(_ => BitcoinFactory.CreateOutPoint()).ToArray();
		prison.FailedToSign(outpointsToBan[0], Money.Coins(0.005m), roundId);
		var banningTimeFrames = outpointsToBan.Zip(outpointsToBan[1..], (a, b) => new { Destroyed = a, Created = b })
			.Select(x =>
			{
				prison.InheritPunishment(x.Created, new[] { x.Destroyed });
				return prison.GetBanTimePeriod(x.Created, cfg);
			}).ToArray();

		// Every time it is banned, the duration is halfed
		var banningTimeFramePairs = banningTimeFrames.Zip(banningTimeFrames[1..], (parent, child) => (Parent: parent, Child: child)).ToArray();
		Assert.All(banningTimeFramePairs[..^1], x => Assert.Equal(x.Parent.Duration, x.Child.Duration * 2));

		// Final time frame is always zero
		Assert.Equal(TimeSpan.Zero, banningTimeFrames[^1].Duration);
	}
}
