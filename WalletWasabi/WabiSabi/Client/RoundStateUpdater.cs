using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.ArenaDomain;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class RoundStateUpdater : PeriodicRunner
	{
		public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
		{
			ArenaRequestHandler = arenaRequestHandler;
		}

		private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		private Dictionary<uint256, RoundState2> RoundStates { get; set; } = new();
		private Dictionary<uint256, long> RoundLastSequenceIds { get; set; } = new();
		private Dictionary<uint256, RoundAggregate> RoundAggregates { get; set; } = new();

		private List<RoundStateAwaiter> Awaiters { get; } = new();
		private object AwaitersLock { get; } = new();

		public bool AnyRound => RoundStates.Any();

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			var statusResponse = await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false);
			var roundIds = statusResponse.Select(s => s.Id);

			foreach (var roundId in roundIds)
			{
				RoundLastSequenceIds.TryAdd(roundId, 0);
				var lastSequenceId = RoundLastSequenceIds[roundId];
				var newEvents = await ArenaRequestHandler.GetRoundEvents(roundId.ToString(), lastSequenceId, cancellationToken).ConfigureAwait(false);

				if (newEvents.LastOrDefault() is { SequenceId: var sequenceId })
				{
					if (!RoundAggregates.TryGetValue(roundId, out var roundAggregate))
					{
						roundAggregate = new RoundAggregate();
						RoundAggregates.Add(roundId, roundAggregate);
					}

					foreach (var wrappedEvent in newEvents)
					{
						roundAggregate.Apply(wrappedEvent.DomainEvent);
					}

					RoundLastSequenceIds[roundId] = sequenceId;

					RoundStates[roundId] = roundAggregate.State;
				}
			}

			lock (AwaitersLock)
			{
				foreach (var awaiter in Awaiters.Where(awaiter => awaiter.IsCompleted(RoundStates)).ToArray())
				{
					// The predicate was fulfilled.
					Awaiters.Remove(awaiter);
					break;
				}
			}
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
