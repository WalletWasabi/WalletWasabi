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
	public Prison(ICoinJoinIdStore coinJoinIdStore, IEnumerable<Offender> offenders, ChannelWriter<Offender> channelWriterWriter)
	{
		CoinJoinIdStore = coinJoinIdStore;
		OffendersByTxId = offenders.GroupBy(x => x.OutPoint.Hash).ToDictionary(x => x.Key, x => x.ToList());
		NotificationChannelWriter = channelWriterWriter;
	}

	private ICoinJoinIdStore CoinJoinIdStore { get; }
	private ChannelWriter<Offender> NotificationChannelWriter { get; }
	private Dictionary<uint256, List<Offender>> OffendersByTxId { get; }
	private Dictionary<OutPoint, TimeFrame> BanningTimeCache { get; } = new();

	/// <remarks>Lock object to guard <see cref="OffendersByTxId"/>.</remarks>
	private object Lock { get; } = new();

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

	public void FailedToSignalReadyToSign(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DidNotSignalReadyToSign)));

	public bool IsBanned(OutPoint outpoint, DoSConfiguration configuration, DateTimeOffset when) =>
		GetBanTimePeriod(outpoint, configuration).Includes(when);

	public TimeFrame GetBanTimePeriod(OutPoint outpoint, DoSConfiguration configuration)
	{
		TimeFrame EffectiveMinTimeFrame(Offender offender, TimeSpan banningTime)
		{
			var effectiveBanningTime = banningTime < configuration.MinTimeInPrison
				? TimeSpan.Zero
				: banningTime;
			return new TimeFrame(offender.StartedTime, effectiveBanningTime);
		}

		TimeSpan CalculatePunishment(Offender offender, RoundDisruption disruption)
		{
			var basePunishmentInHours = configuration.SeverityInBitcoinsPerHour / disruption.Value.ToDecimal(MoneyUnit.BTC);

			List<RoundDisruption> offenderHistory;
			lock (Lock)
			{
				offenderHistory = OffendersByTxId.TryGetValue(offender.OutPoint.Hash, out var offenders)
					? offenders
						.Where(x => x.OutPoint == offender.OutPoint)
						.Select(x => x.Offense)
						.OfType<RoundDisruption>()
						.ToList()
					: new List<RoundDisruption>();
			}

			var maxOffense = offenderHistory.Count == 0
				? 1
				: offenderHistory.Max( x => x switch {
					{ Method: RoundDisruptionMethod.DidNotConfirm } => configuration.PenaltyFactorForDisruptingConfirmation,
					{ Method: RoundDisruptionMethod.DidNotSign } => configuration.PenaltyFactorForDisruptingSigning,
					{ Method: RoundDisruptionMethod.DoubleSpent } => configuration.PenaltyFactorForDisruptingByDoubleSpending,
					{ Method: RoundDisruptionMethod.DidNotSignalReadyToSign } => configuration.PenaltyFactorForDisruptingSignalReadyToSign,

					_ => throw new NotSupportedException("Unknown round disruption method.")
				});

			var repetitions = offenderHistory.Count;
			var repetitionFactor = CoinJoinIdStore.Contains(offender.OutPoint.Hash)
				? repetitions                   // Linear punishment
				: Math.Pow(2, repetitions - 1); // Exponential punishment

			var prisonTime = basePunishmentInHours * maxOffense * (decimal)repetitionFactor;
			return TimeSpan.FromHours((double)prisonTime);
		}

		TimeFrame CalculatePunishmentInheritance(OutPoint[] ancestors)
		{
			return ancestors
				.Select(a => (Ancestor: a, BanningTime: GetBanTimePeriod(a, configuration)))
				.MaxBy(x => x.BanningTime.EndTime)
				.BanningTime;
		}

		Offender? offender;
		lock (Lock)
		{
			if (BanningTimeCache.TryGetValue(outpoint, out var cachedBanningTime))
			{
				return cachedBanningTime;
			}

			offender = OffendersByTxId.TryGetValue(outpoint.Hash, out var offenders)
				? offenders.LastOrDefault(x => x.OutPoint == outpoint)
				: null;
		}

		var banningTime = offender switch
		{
			null => TimeFrame.Zero,
			{ Offense: FailedToVerify } => EffectiveMinTimeFrame(offender, configuration.MinTimeForFailedToVerify),
			{ Offense: Cheating } => EffectiveMinTimeFrame(offender, configuration.MinTimeForCheating),
			{ Offense: RoundDisruption offense } => EffectiveMinTimeFrame(offender, CalculatePunishment(offender, offense)),
			{ Offense: Inherited { Ancestors: { } ancestors } } => CalculatePunishmentInheritance(ancestors),
			_ => throw new NotSupportedException("Unknown offense type.")
		};

		lock (Lock)
		{
			BanningTimeCache[outpoint] = banningTime;
		}
		return banningTime;
	}

	public void Punish(Offender offender)
	{
		lock (Lock)
		{
			if (OffendersByTxId.TryGetValue(offender.OutPoint.Hash, out var offenders))
			{
				offenders.Add(offender);
			}
			else
			{
				OffendersByTxId.Add(offender.OutPoint.Hash, new List<Offender> { offender });
			}

			BanningTimeCache.Remove(offender.OutPoint);
		}
		if (!NotificationChannelWriter.TryWrite(offender))
		{
			Logger.LogWarning($"Failed to persist offender '{offender.OutPoint}'.");
		}
	}
}
