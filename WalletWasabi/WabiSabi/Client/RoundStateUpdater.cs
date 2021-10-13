using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
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
		private Dictionary<uint256, RoundState> RoundStates { get; set; } = new();

		private List<RoundStateAwaiter> Awaiters { get; } = new();
		private object AwaitersLock { get; } = new();

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			var request = new RoundStateRequest(RoundStates.Select(x => (x.Key, x.Value.CoinjoinState.Order)).ToImmutableList());
			RoundState[] statusResponse = await ArenaRequestHandler.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);

			var updatedRoundStates = statusResponse
				.Where(rs => RoundStates.ContainsKey(rs.Id))
				.Select(rs => (Update: rs, CurrentRoundState: RoundStates[rs.Id]))
				.Select(x => x.Update with {
					CoinjoinState = x.Update.CoinjoinState.MergeBack(x.CurrentRoundState.CoinjoinState)
					}).ToList();

			var newRoundStates = statusResponse
				.Where(rs => !RoundStates.ContainsKey(rs.Id));

			RoundStates = newRoundStates.Concat(updatedRoundStates).ToDictionary(x => x.Id, x => x);

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

		public Task<RoundState> CreateRoundAwaiter(uint256? roundId, Predicate<RoundState> predicate, CancellationToken cancellationToken)
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

		public Task<RoundState> CreateRoundAwaiter(Predicate<RoundState> predicate, CancellationToken cancellationToken)
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
