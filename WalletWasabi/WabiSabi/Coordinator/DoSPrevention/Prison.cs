using System.Threading.Channels;

namespace WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

public class Prison
{
	private static readonly TimeSpan CachePruningInterval = TimeSpan.FromHours(1);
	private static readonly TimeSpan CacheExpirationGracePeriod = TimeSpan.FromHours(4);

	public Prison(IEnumerable<Offender> offenders, ChannelWriter<Offender> channelWriterWriter)
	{
		_offendersByTxId = offenders
			.GroupBy(x => x.OutPoint.Hash)
			.ToDictionary(x => x.Key, x => x.ToList());

		_notificationChannelWriter = channelWriterWriter;
	}

	private readonly ChannelWriter<Offender> _notificationChannelWriter;
	private readonly Dictionary<uint256, List<Offender>> _offendersByTxId;
	private readonly Dictionary<OutPoint, TimeFrame> _banningTimeCache = new();
	private DateTimeOffset _lastCachePruning;

	/// <remarks>Lock object to guard <see cref="_lastCachePruning"/>, <see cref="_offendersByTxId"/> and <see cref="_banningTimeCache"/>.</remarks>
	private readonly Lock _lock = new();

	public void CoordinatorStabilitySafetyBan(OutPoint outPoint, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new CoordinatorStabilitySafety(roundId)));

	public void FailedToConfirm(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DidNotConfirm)));

	public void FailedToSign(OutPoint outPoint, Money value, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new RoundDisruption(roundId, value, RoundDisruptionMethod.DidNotSign)));

	public void FailedVerification(OutPoint outPoint, uint256 roundId) =>
		Punish(new Offender(outPoint, DateTimeOffset.UtcNow, new FailedToVerify(roundId)));

	public void CheatingDetected(OutPoint outPoint, uint256 roundId) =>
		CheatingDetected(outPoint, roundId, DateTimeOffset.UtcNow);

	public void CheatingDetected(OutPoint outPoint, uint256 roundId, DateTimeOffset startTime) =>
		Punish(new Offender(outPoint, startTime, new Cheating(roundId)));

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
		lock (_lock)
		{
			return GetBanTimePeriodNoLock(outpoint, configuration);
		}
	}

	private TimeFrame GetBanTimePeriodNoLock(OutPoint outpoint, DoSConfiguration configuration)
	{
		TimeFrame EffectiveMinTimeFrame(TimeFrame banningPeriod) =>
			banningPeriod.Duration < configuration.MinTimeInPrison
				? TimeFrame.Zero
				: banningPeriod;

		TimeSpan CalculatePunishmentNoLock(Offender offender, RoundDisruption disruption)
		{
			var basePunishmentInHours = configuration.SeverityInBitcoinsPerHour / disruption.Value.ToDecimal(MoneyUnit.BTC);

			IReadOnlyList<RoundDisruption> offenderHistory = _offendersByTxId.TryGetValue(offender.OutPoint.Hash, out var offenders)
				? offenders
					.Where(x => x.OutPoint.N == offender.OutPoint.N)
					.Select(x => x.Offense)
					.OfType<RoundDisruption>()
					.ToList()
				: Array.Empty<RoundDisruption>();

			var maxOffense = offenderHistory.Count == 0
				? 1
				: offenderHistory.Max(x => x switch {
					{ Method: RoundDisruptionMethod.DidNotConfirm } => configuration.PenaltyFactorForDisruptingConfirmation,
					{ Method: RoundDisruptionMethod.DidNotSign } => configuration.PenaltyFactorForDisruptingSigning,
					{ Method: RoundDisruptionMethod.DoubleSpent } => configuration.PenaltyFactorForDisruptingByDoubleSpending,
					{ Method: RoundDisruptionMethod.DidNotSignalReadyToSign } => configuration.PenaltyFactorForDisruptingSignalReadyToSign,

					_ => throw new NotSupportedException("Unknown round disruption method.")
				});

			var repetitions = offenderHistory.Count;
			var repetitionFactor = Math.Pow(1.3, repetitions - 1); // Exponential punishment

			var prisonTime = basePunishmentInHours * maxOffense * (decimal)repetitionFactor;
			return TimeSpan.FromHours((double)prisonTime);
		}

		TimeFrame CalculatePunishmentInheritance(OutPoint[] ancestors)
		{
			var banningTimeFrame = ancestors
				.Select(a => (Ancestor: a, BanningTime: GetBanTimePeriodNoLock(a, configuration)))
				.MaxBy(x => x.BanningTime.EndTime)
				.BanningTime;
			return new TimeFrame(banningTimeFrame.StartTime, banningTimeFrame.Duration / 2);
		}

		Offender? offender;
		if (_banningTimeCache.TryGetValue(outpoint, out var cachedBanningTime))
		{
			return cachedBanningTime;
		}

		offender = _offendersByTxId.TryGetValue(outpoint.Hash, out var offenders)
			? offenders.LastOrDefault(x => x.OutPoint == outpoint)
			: null;

		// A majority of UTXOs is not banned. A minority is banned.
		if (offender is null)
		{
			return TimeFrame.Zero;
		}

		TimeFrame banningTime = EffectiveMinTimeFrame(offender switch
		{
			{ Offense: CoordinatorStabilitySafety } => new TimeFrame(offender.StartedTime, configuration.MinTimeInPrison + TimeSpan.FromHours(Random.Shared.Next(0, 4))),
			{ Offense: FailedToVerify } => new TimeFrame(offender.StartedTime, configuration.MinTimeForFailedToVerify),
			{ Offense: Cheating } => new TimeFrame(offender.StartedTime, configuration.MinTimeForCheating),
			{ Offense: RoundDisruption offense } => new TimeFrame(offender.StartedTime, CalculatePunishmentNoLock(offender, offense)),
			{ Offense: Inherited { Ancestors: { } ancestors } } => CalculatePunishmentInheritance(ancestors),
			_ => throw new NotSupportedException("Unknown offense type.")
		});

		_banningTimeCache[outpoint] = banningTime;
		return banningTime;
	}

	private void Punish(Offender offender)
	{
		lock (_lock)
		{
			if (_offendersByTxId.TryGetValue(offender.OutPoint.Hash, out var offenders))
			{
				offenders.Add(offender);
			}
			else
			{
				_offendersByTxId.Add(offender.OutPoint.Hash, new List<Offender> { offender });
			}

			_ = _banningTimeCache.Remove(offender.OutPoint);

			PruneExpiredBansNoLock(DateTimeOffset.UtcNow);
		}

		if (!_notificationChannelWriter.TryWrite(offender))
		{
			Logger.LogWarning($"Failed to persist offender '{offender.OutPoint}'.");
		}
	}

	/// <summary>
	/// Removes cached ban periods that expired long ago.
	/// </summary>
	private void PruneExpiredBansNoLock(DateTimeOffset now)
	{
		// Run this cleanup routine once in a while.
		if (now - _lastCachePruning < CachePruningInterval)
		{
			return;
		}

		_lastCachePruning = now;

		foreach (var (outpoint, period) in _banningTimeCache.ToArray())
		{
			if (period.EndTime < now - CacheExpirationGracePeriod)
			{
				_ = _banningTimeCache.Remove(outpoint);
			}
		}
	}
}
