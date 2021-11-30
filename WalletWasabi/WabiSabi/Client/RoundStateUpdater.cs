using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.ArenaDomain;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client
{
	public class RoundStateUpdater : PeriodicRunner
	{
		public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
		{
			ArenaRequestHandler = arenaRequestHandler;
		}

		private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		private Dictionary<uint256, (long LastSequenceId, RoundState2 State, RoundAggregate Aggregate)> ActiveRounds { get; set; } = new();

		private List<RoundStateAwaiter> Awaiters { get; } = new();
		private object AwaitersLock { get; } = new();

		public IBackendHttpClientFactory BackendHttpClientFactory { get; }

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			List<uint256> interestingRoundIds = new();

			if (Awaiters.Any(a => a.RoundId is null))
			{
				// We are interested about every round.
				var statusResponse = await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false);
				interestingRoundIds.AddRange(statusResponse.Select(s => s.Id));
			}
			else
			{
				IEnumerable<uint256> roundIds = Awaiters.Select(a => a.RoundId).OfType<uint256>();
				if (roundIds.Any())
				{
					interestingRoundIds.AddRange(roundIds);
				}
			}

			var interestingRoundIdsHashSet = interestingRoundIds.ToHashSet();
			var notInterestingRoundIds = ActiveRounds.Select(r => r.Key).Where(id => !interestingRoundIdsHashSet.Contains(id));

			if (!interestingRoundIds.Any())
			{
				return;
			}

			foreach (var roundId in interestingRoundIds)
			{
				if (!ActiveRounds.ContainsKey(roundId))
				{
					ActiveRounds.Add(roundId, (0, new RoundState2(), new RoundAggregate()));
				}

				var lastSequenceId = ActiveRounds[roundId].LastSequenceId;
				var newEvents = await ArenaRequestHandler.GetRoundEvents(roundId.ToString(), lastSequenceId, cancellationToken).ConfigureAwait(false);

				if (newEvents.LastOrDefault() is { SequenceId: var newSequenceId })
				{
					var roundAggregate = ActiveRounds[roundId].Aggregate;
					foreach (var wrappedEvent in newEvents)
					{
						roundAggregate.Apply(wrappedEvent.DomainEvent);
					}

					ActiveRounds[roundId] = (newSequenceId, roundAggregate.State, roundAggregate);
				}
			}

			lock (AwaitersLock)
			{
				foreach (var awaiter in Awaiters.Where(awaiter => awaiter.IsCompleted(ActiveRounds.ToDictionary(r => r.Key, r => r.Value.State))).ToArray())
				{
					// The predicate was fulfilled.
					Awaiters.Remove(awaiter);
					break;
				}
			}

			foreach (var round in ActiveRounds.Where(r => r.Value.State.Phase == Phase.Ended).Select(r => r.Key).ToArray())
			{
				ActiveRounds.Remove(round);
			}
		}

		public Task<RoundState2> CreateRoundAwaiter(uint256 roundId, Phase phase, CancellationToken cancellationToken)
		{
			return CreateRoundAwaiter(roundId, round => round.Phase == phase, cancellationToken);
		}

		public Task<RoundState2> CreateRoundAwaiter(uint256? roundId, Predicate<RoundState2> predicate, CancellationToken cancellationToken)
		{
			RoundStateAwaiter? roundStateAwaiter = null;

			lock (AwaitersLock)
			{
				roundStateAwaiter = new RoundStateAwaiter(predicate, roundId, cancellationToken);
				Awaiters.Add(roundStateAwaiter);
			}

			cancellationToken.Register(() =>
			{
				lock (AwaitersLock)
				{
					Awaiters.Remove(roundStateAwaiter);
				}
			});

			return roundStateAwaiter.Task;
		}

		public Task<RoundState2> CreateRoundAwaiter(Predicate<RoundState2> predicate, CancellationToken cancellationToken)
		{
			return CreateRoundAwaiter(null, predicate, cancellationToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken)
		{
			lock (AwaitersLock)
			{
				foreach (var awaiter in Awaiters)
				{
					awaiter.Cancel();
				}
			}
			return base.StopAsync(cancellationToken);
		}
	}
}
