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

		private Dictionary<uint256, List<(TaskCompletionSource<RoundState> Task, Predicate<RoundState> Predicate)>> Awaiters { get; } = new();

		public RoundStatusUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
		{
			ArenaRequestHandler = arenaRequestHandler;
		}

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			var statusResponse = await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false);
			var responseRoundStates = statusResponse.ToDictionary(round => round.Id);

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
				roundsToUpdate.Add(round.Id);
			}

			RoundStates = updatedRoundStates.Union(newRoundStates).ToDictionary(s => s.Key, s => s.Value);

			if (roundsToUpdate.Any())
			{
				ExecuteAwaiters(roundsToUpdate, RoundStates);
			}
		}

		private void ExecuteAwaiters(
			IEnumerable<uint256> roundsToUpdate,
			Dictionary<uint256, RoundState> roundStates)
		{
			foreach (var roundId in roundsToUpdate)
			{
				if (roundStates.TryGetValue(roundId, out RoundState? roundState))
				{
					// The round is missing.
					var tasks = Awaiters[roundId];
					foreach (var t in tasks)
					{
						t.Task.TrySetException(new InvalidOperationException($"Round {roundId} is not running anymore."));
					}
					Awaiters.Remove(roundId);
					continue;
				}

				var taskAndPredicateList = Awaiters[roundId];
				foreach (var taskAndPredicate in taskAndPredicateList.Where(taskAndPredicate => taskAndPredicate.Predicate(roundState!)).ToArray())
				{
					// The predicate was fulfilled.
					var task = taskAndPredicate.Task;
					task.TrySetResult(roundState!);
					taskAndPredicateList.Remove(taskAndPredicate);
				}
			}
		}

		public Task<RoundState> CreateRoundAwaiter(uint256 roundId, Predicate<RoundState> predicate, CancellationToken cancellationToken)
		{
			TaskCompletionSource<RoundState> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!Awaiters.ContainsKey(roundId))
			{
				Awaiters.Add(roundId, new List<(TaskCompletionSource<RoundState>, Predicate<RoundState>)>());
			}
			var predicateList = Awaiters[roundId];
			var taskAndPredicate = (tcs, predicate);
			predicateList.Add(taskAndPredicate);

			cancellationToken.Register(() =>
			{
				tcs.TrySetCanceled();
				predicateList.Remove(taskAndPredicate);
			});

			return tcs.Task;
		}
	}
}
