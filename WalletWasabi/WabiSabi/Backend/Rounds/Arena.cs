using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Arena : PeriodicRunner
	{
		public Arena(TimeSpan period, Network network, WabiSabiConfig config, IRPCClient rpc, Prison prison) : base(period)
		{
			Network = network;
			Config = config;
			Rpc = rpc;
			Prison = prison;
			Random = new SecureRandom();

			Rounds = new() { ConcurrentDictionary = RoundsById, KeyFunc = r => r.Id };
		}

		private ConcurrentDictionary<uint256, Round> RoundsById { get; } = new();
		[ObsoleteAttribute("Access to internal Arena state should be removed from tests.")]
		internal ConcurrentDictionaryValueCollectionView<Round> Rounds { get; }
		private ConcurrentDictionary<OutPoint, Alice> AlicesByOutpoint { get; } = new();
		private ConcurrentDictionary<uint256, Alice> AlicesById { get; } = new();
		private AsyncLock AsyncLock { get; } = new();
		public Network Network { get; }
		public WabiSabiConfig Config { get; }
		public IRPCClient Rpc { get; }
		public Prison Prison { get; }
		public SecureRandom Random { get; }

		public IEnumerable<Round> ActiveRounds => Rounds.Where(x => x.Phase != Phase.Ended);

		private void RemoveRound(Round round)
		{
			RoundsById.Remove(round.Id, out _);
			Rounds.Remove(round);

			foreach (var alice in AlicesById.Values.Where(alice => alice.Round == round))
			{
				RemoveAlice(alice);
			}

			RoundsById.Remove(round.Id, out _);
		}

		public void RemoveAlice(Alice alice)
		{
			AlicesById.Remove(alice.Id, out _);
			AlicesByOutpoint.Remove(alice.Coin.Outpoint, out _);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				TimeoutRounds();
			}

			await TimeoutAlicesAsync(cancel);

			foreach (var round in ActiveRounds)
			{
				// FIXME remove, hack to make alices injected by tests accessible from requests
				foreach (var alice in round.Alices.Where(alice => !AlicesById.ContainsKey(alice.Id)))
				{
					if (!AlicesByOutpoint.TryAdd(alice.Coin.Outpoint, alice) || !AlicesById.TryAdd(alice.Id, alice))
					{
						throw new InvalidOperationException();
					}
				}
			}

			foreach (var round in RoundsById.Select(x => x.Value))
			{
				await round.StepAsync(this, cancel);

				cancel.ThrowIfCancellationRequested();

				// Ensure there's at least one non-blame round in input registration.
				await CreateRoundsAsync(cancel).ConfigureAwait(false);
			}

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);
		}

		private void TimeoutRounds()
		{
		    foreach (var expiredRound in RoundsById.Select(x => x.Value).Where(
						 x =>
						 x.Phase == Phase.Ended
						 && x.End + Config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
			{
				RemoveRound(expiredRound);
			}
		}

		// TODO make alice time itself out by running its own background task
		// that terminates when alice is confirmed or removed
		private async Task TimeoutAlicesAsync(CancellationToken cancel)
		{
			foreach (var alice in AlicesById.Select(x => x.Value))
			{
				using (await alice.AsyncLock.LockAsync(cancel).ConfigureAwait(false))
				{
					await alice.Round.TimeoutAliceAsync(alice, this, cancel);
				}
			}
		}

		public async Task CreateBlameRoundAsync(Round round, CancellationToken cancel)
		{
			var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = new(Config, Network, Random, feeRate, blameOf: round);
			await AddRound(new(parameters), cancel);
		}

		private async Task CreateRoundsAsync(CancellationToken cancel)
		{
			// Ensure there is always a round accepting inputs
			if (!RoundsById.Select(x => x.Value).Any(x => !x.IsBlameRound && x.Phase == Phase.InputRegistration))
			{
				var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true).ConfigureAwait(false)).FeeRate;
				RoundParameters roundParams = new(Config, Network, Random, feeRate);
				await AddRound(new(roundParams), cancel);
			}
		}

		private async Task AddRound(Round round, CancellationToken cancel)
		{
			if (!RoundsById.TryAdd(round.Id, round))
			{
				throw new InvalidOperationException();
			}
		}

		public async Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
		{
			return RoundsById.Select(x => RoundState.FromRound(x.Value)).ToArray();
		}

		public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			var coin = await OutpointToCoinAsync(request, cancellationToken).ConfigureAwait(false);

			var alice = new Alice(coin, request.OwnershipProof, round);

			// Begin with Alice locked, to serialize requests concerning a
			// single coin.
			using (await alice.AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				var otherAlice = AlicesByOutpoint.GetOrAdd(coin.Outpoint, alice);
				if (otherAlice != alice)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
				}

				var inputRegistered = false;

				try
				{
					var response = await round.RegisterInputAsync(alice, request, Config);

					// Now that alice is in the round, make it available by id.
					if (!AlicesById.TryAdd(alice.Id, alice))
					{
						throw new InvalidOperationException($"Alice {alice.Id} already exists.");
					}

					inputRegistered = true;

					return response;
				}
				finally
				{
					if (!inputRegistered)
					{
						AlicesByOutpoint.Remove(coin.Outpoint, out _);
					}
				}
			}
		}

		private async Task<Coin> OutpointToCoinAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
		{
			OutPoint input = request.Input;

			if (Prison.TryGet(input, out var inmate) && (!Config.AllowNotedInputRegistration || inmate.Punishment != Punishment.Noted))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputBanned);
			}

			var txOutResponse = await Rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true).ConfigureAwait(false);
			if (txOutResponse is null)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputSpent);
			}
			if (txOutResponse.Confirmations == 0)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputUnconfirmed);
			}
			if (txOutResponse.IsCoinBase && txOutResponse.Confirmations <= 100)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputImmature);
			}

			return new Coin(input, txOutResponse.TxOut);
		}

		public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			if (!AlicesById.TryGetValue(request.AliceId, out var alice) || alice.Round != round)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
			}

			using (await alice.AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!AlicesById.ContainsKey(alice.Id))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
				}

				await round.ReadyToSignAsync(alice, request);
			}
		}

		public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			if (!AlicesById.TryGetValue(request.AliceId, out var alice) || alice.Round != round)
			{
				// Idempotent removal
				return;
			}

			if (alice.Round != round)
			{
				// Alice exists, but not in this round
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
			}

			using (await alice.AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!AlicesById.ContainsKey(alice.Id))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
				}

				await round.RemoveInputAsync(alice, this, request);
			}
		}

		public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			if (!AlicesById.TryGetValue(request.AliceId, out var alice) || alice.Round != round)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
			}

			using (await alice.AsyncLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!AlicesById.ContainsKey(alice.Id))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound, $"Round ({request.RoundId}): Alice ({request.AliceId}) not found.");
				}

				return await round.ConfirmConnectionAsync(alice, request);
			}
		}

		public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			await round.RegisterOutputAsync(request);
		}

		public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			await round.SignTransactionAsync(request);
		}

		public ReissueCredentialResponse ReissueCredentials(ReissueCredentialRequest request)
		{
			if (!RoundsById.TryGetValue(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({request.RoundId}) not found.");
			}

			return round.ReissueCredentials(request);
		}

		public override void Dispose()
		{
			Random.Dispose();
			base.Dispose();
		}
	}
}
