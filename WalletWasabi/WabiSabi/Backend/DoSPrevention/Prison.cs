using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.WabiSabi.Backend.DoSPrevention;

public class Prison
{
	public Prison(DoSConfiguration dosConfiguration, ICoinJoinIdStore coinJoinIdStore, IEnumerable<Offender> offenders, ChannelWriter<Offender> channelWriterWriter)
	{
		DoSConfiguration = dosConfiguration;
		CoinJoinIdStore = coinJoinIdStore;
		Offenders = offenders.ToList();
		NotificationChannelWriter = channelWriterWriter;
	}

	private ICoinJoinIdStore CoinJoinIdStore { get; }
	private DoSConfiguration DoSConfiguration { get; }
	private ChannelWriter<Offender> NotificationChannelWriter { get; }
	private List<Offender> Offenders { get; }

	/// <remarks>Lock object to guard <see cref="Offenders"/>.</remarks>
	private object Lock { get; } = new();

	private void Punish(Offender offender)
	{
		lock (Lock)
		{
			Offenders.Add(offender);
		}
		if (!NotificationChannelWriter.TryWrite(offender))
		{
			Logger.LogWarning($"Failed to persist offender '{offender.OutPoint}'.");
		}
	}

	public void FailedToConfirm(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DidNotConfirm)));

	public void FailedToSign(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DidNotSign)));

	public void FailedVerification(OutPoint outPoint, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new FailedToVerify(roundId)));

	public void CheatingDetected(OutPoint outPoint, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new Cheating(roundId)));

	public void DoubleSpent(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DoubleSpent)));

	public void InheritPunishment(OutPoint outpoint, OutPoint[] ancestors) =>
		Punish(new Offender(outpoint, DateTimeOffset.UtcNow, new Inherited(ancestors)));

	public bool IsBanned(OutPoint outpoint, DateTimeOffset when) =>
		GetBanTimePeriod(outpoint).Includes(when);

	public TimeFrame GetBanTimePeriod(OutPoint outpoint)
	{
		Offender? offender;
		lock (Lock)
		{
			offender = Offenders.LastOrDefault(x => x.OutPoint == outpoint);
		}

		return offender switch
		{
			null => TimeFrame.Zero,
			{ Offense: FailedToVerify } => EffectiveMinTimeFrame(offender, DoSConfiguration.MinTimeForFailedToVerify),
			{ Offense: Cheating } => EffectiveMinTimeFrame(offender, DoSConfiguration.MinTimeForCheating),
			{ Offense: RoundDisruption offense } => EffectiveMinTimeFrame(offender, CalculatePunishment(offender, offense)),
			{ Offense: Inherited { Ancestors: { } ancestors } } => CalculatePunishmentInheritance(ancestors),
			_ => throw new NotSupportedException("Unknown offense type.")
		};
	}

	private TimeFrame EffectiveMinTimeFrame(Offender offender, TimeSpan banningTime)
	{
		var effectiveBanningTime = banningTime < DoSConfiguration.MinTimeInPrison
			? TimeSpan.Zero
			: banningTime;
		return new TimeFrame(offender.StartedTime, effectiveBanningTime);
	}

	private TimeSpan CalculatePunishment(Offender offender, RoundDisruption disruption)
	{
		var basePunishmentInHours = DoSConfiguration.SeverityInBitcoinsPerHour / disruption.Value.ToDecimal(MoneyUnit.BTC);

		if (CoinJoinIdStore.Contains(offender.OutPoint.Hash))
		{
			return TimeSpan.FromHours((double)basePunishmentInHours);
		}

		List<Offender> offenderHistory;
		lock (Lock)
		{
			offenderHistory = Offenders.Where(x => x.OutPoint == offender.OutPoint).ToList();
		}

		var maxOffense = offenderHistory.Max(
			x => disruption switch
			{
				{ Method: RoundDisruptionMethod.DidNotConfirm } => DoSConfiguration.PenaltyFactorForDisruptingConfirmation,
				{ Method: RoundDisruptionMethod.DidNotSign } => DoSConfiguration.PenaltyFactorForDisruptingSigning,
				{ Method: RoundDisruptionMethod.DoubleSpent } => DoSConfiguration.PenaltyFactorForDisruptingByDoubleSpending,
				_ => throw new NotSupportedException("Unknown round disruption method.")
			});

		var prisonTime = basePunishmentInHours * maxOffense * (decimal)Math.Pow(2, offenderHistory.Count - 1);
		return TimeSpan.FromHours((double)prisonTime);
	}

	private TimeFrame CalculatePunishmentInheritance(OutPoint[] ancestors) =>
		ancestors
			.Select(a => (Ancestor: a, BanningTime: GetBanTimePeriod(a)))
			.MaxBy(x => x.BanningTime.EndTime)
			.BanningTime;
}
