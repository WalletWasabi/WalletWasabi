using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class RoundStatusUpdater : PeriodicRunner
	{
		private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		private Dictionary<uint256, RoundState> RoundStates { get; set; } = new();

		private Dictionary<uint256, List<(TaskCompletionSource<RoundState> Task, Predicate<RoundState> Predicate)>> Awaiters { get; set; } = new();

		public RoundStatusUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
		{
			ArenaRequestHandler = arenaRequestHandler;
		}

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			var responseRoundStates = (await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false))
				.ToDictionary(round => round.Id);

			var updatedRoundStates = responseRoundStates.Where(round => RoundStates.ContainsKey(round.Key));
			var newRoundStates = responseRoundStates.Where(round => !RoundStates.ContainsKey(round.Key));
			var removedRoundStates = RoundStates.Where(round => !responseRoundStates.ContainsKey(round.Key));

			List<uint256> roundsToUpdate = new();

			foreach (var updatedRound in updatedRoundStates.Select(round => round.Value))
			{
				var oldRound = RoundStates[updatedRound.Id];
				if (oldRound != updatedRound)
				{
					roundsToUpdate.Add(updatedRound.Id);
				}
			}

			foreach (var round in newRoundStates.Select(round => round.Value))
			{
				roundsToUpdate.Add(round.Id);
			}

			foreach (var round in removedRoundStates.Select(round => round.Value))
			{
				// TODO: set all associated tasks to failed.
			}

			RoundStates = updatedRoundStates.Union(newRoundStates).ToImmutableDictionary();

			ExecuteAwaiters(roundsToUpdate, RoundStates);
		}

		private void ExecuteAwaiters(
			IEnumerable<uint256> roundsToUpdate,
			ImmutableDictionary<uint256, RoundState> roundStates)
		{
			foreach (var roundId in roundsToUpdate.Where(rid => roundStates.ContainsKey(rid) && Awaiters.ContainsKey(rid)))
			{
				var roundState = roundStates[roundId];
				var taskAndPredicateList = Awaiters[roundId];
				foreach (var taskAndPredicate in taskAndPredicateList.ToArray())
				{
					if (taskAndPredicate.Predicate(roundState))
					{
						var task = taskAndPredicate.Task;
						task.TrySetResult(roundState);
						taskAndPredicateList.Remove(taskAndPredicate);
					}
				}
			}
		}

		public Task<RoundState> CreateRoundAwaiter(uint256 roundId, Predicate<RoundState> predicate)
		{
			// TODO: Add cancellationToken
			var tcs = new TaskCompletionSource<RoundState>();
			if (!Awaiters.ContainsKey(roundId))
			{
				Awaiters.Add(roundId, new List<(TaskCompletionSource<RoundState>, Predicate<RoundState>)>());
			}
			var predicateList = Awaiters[roundId];
			predicateList.Add((tcs, predicate));
			return tcs.Task;
		}
	}
}
