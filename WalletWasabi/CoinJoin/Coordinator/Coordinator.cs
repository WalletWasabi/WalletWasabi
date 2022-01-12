using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using WalletWasabi.CoinJoin.Coordinator.Participants;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.CoinJoin.Coordinator;

public class Coordinator : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	public Coordinator(Network network, BlockNotifier blockNotifier, string folderPath, IRPCClient rpc, CoordinatorRoundConfig roundConfig)
	{
		Network = Guard.NotNull(nameof(network), network);
		BlockNotifier = Guard.NotNull(nameof(blockNotifier), blockNotifier);
		FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
		RpcClient = Guard.NotNull(nameof(rpc), rpc);
		RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

		Rounds = ImmutableList<CoordinatorRound>.Empty;

		LastSuccessfulCoinJoinTime = DateTimeOffset.UtcNow;

		Directory.CreateDirectory(FolderPath);

		UtxoReferee = new UtxoReferee(Network, FolderPath, RpcClient, RoundConfig);

		if (File.Exists(CoinJoinsFilePath))
		{
			try
			{
				var getTxTasks = new List<(Task<Transaction> txTask, string line)>();
				var batch = RpcClient.PrepareBatch();

				var toRemove = new List<string>();
				string[] allLines = File.ReadAllLines(CoinJoinsFilePath);
				foreach (string line in allLines)
				{
					try
					{
						getTxTasks.Add((batch.GetRawTransactionAsync(uint256.Parse(line)), line));
					}
					catch (Exception ex)
					{
						toRemove.Add(line);

						var logEntry = ex is RPCException rpce && rpce.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY
							? $"CoinJoins file contains invalid transaction ID {line}"
							: $"CoinJoins file got corrupted. Deleting offending line \"{line[..20]}\".";

						Logger.LogWarning($"{logEntry}. {ex.GetType()}: {ex.Message}");
					}
				}

				batch.SendBatchAsync().GetAwaiter().GetResult();

				foreach (var (txTask, line) in getTxTasks)
				{
					try
					{
						var tx = txTask.GetAwaiter().GetResult();
						CoinJoins.Add(tx.GetHash());
					}
					catch (Exception ex)
					{
						toRemove.Add(line);

						var logEntry = ex is RPCException rpce && rpce.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY
							? $"CoinJoins file contains invalid transaction ID {line}"
							: $"CoinJoins file got corrupted. Deleting offending line \"{line[..20]}\".";

						Logger.LogWarning($"{logEntry}. {ex.GetType()}: {ex.Message}");
					}
				}

				if (toRemove.Count != 0) // a little performance boost, it'll be empty almost always
				{
					var newAllLines = allLines.Where(x => !toRemove.Contains(x));
					File.WriteAllLines(CoinJoinsFilePath, newAllLines);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"CoinJoins file got corrupted. Deleting {CoinJoinsFilePath}. {ex.GetType()}: {ex.Message}");
				File.Delete(CoinJoinsFilePath);
			}

			uint256[] mempoolHashes = RpcClient.GetRawMempoolAsync().GetAwaiter().GetResult();
			UnconfirmedCoinJoins.AddRange(CoinJoins.Intersect(mempoolHashes));
		}

		try
		{
			string roundCountFilePath = Path.Combine(folderPath, "RoundCount.txt");
			if (File.Exists(roundCountFilePath))
			{
				string roundCount = File.ReadAllText(roundCountFilePath);
				CoordinatorRound.RoundCount = long.Parse(roundCount);
			}
			else
			{
				// First time initializes (so the first constructor will increment it and we'll start from 1.)
				CoordinatorRound.RoundCount = 0;
			}
		}
		catch (Exception ex)
		{
			CoordinatorRound.RoundCount = 0;
			Logger.LogInfo($"{nameof(CoordinatorRound.RoundCount)} file was corrupt. Resetting to 0.");
			Logger.LogDebug(ex);
		}

		BlockNotifier.OnBlock += BlockNotifier_OnBlockAsync;
	}

	public event EventHandler<Transaction>? CoinJoinBroadcasted;

	public DateTimeOffset LastSuccessfulCoinJoinTime { get; private set; }

	private ImmutableList<CoordinatorRound> Rounds { get; set; }

	private List<uint256> CoinJoins { get; } = new List<uint256>();
	public string CoinJoinsFilePath => Path.Combine(FolderPath, $"CoinJoins{Network}.txt");
	private AsyncLock CoinJoinsLock { get; } = new AsyncLock();

	private List<uint256> UnconfirmedCoinJoins { get; } = new List<uint256>();
	private object UnconfirmedCoinJoinsLock { get; } = new object();

	public IRPCClient RpcClient { get; }

	public CoordinatorRoundConfig RoundConfig { get; private set; }

	public Network Network { get; }

	public BlockNotifier BlockNotifier { get; }

	public string FolderPath { get; }

	public UtxoReferee UtxoReferee { get; }

	private async void BlockNotifier_OnBlockAsync(object? sender, Block block)
	{
		try
		{
			using (await CoinJoinsLock.LockAsync())
			{
				foreach (Transaction tx in block.Transactions)
				{
					await ProcessConfirmedTransactionAsync(tx).ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	public async Task ProcessConfirmedTransactionAsync(Transaction tx)
	{
		// This should not be needed until we would only accept unconfirmed CJ outputs an no other unconf outs. But it'll be more bulletproof for future extensions.
		// Turns out you shouldn't accept RBF at all never. (See below.)

		// https://github.com/zkSNACKs/WalletWasabi/issues/145
		// if it spends a banned output AND it's not CJ output
		// ban all the outputs of the transaction
		tx.PrecomputeHash(false, true);

		lock (UnconfirmedCoinJoinsLock)
		{
			UnconfirmedCoinJoins.Remove(tx.GetHash());
		}

		if (RoundConfig.DosSeverity <= 1)
		{
			return;
		}

		foreach (TxIn input in tx.Inputs)
		{
			OutPoint prevOut = input.PrevOut;

			// if coin is not banned
			var foundElem = await UtxoReferee.TryGetBannedAsync(prevOut, notedToo: true).ConfigureAwait(false);
			if (foundElem is { })
			{
				if (!AnyRunningRoundContainsInput(prevOut, out _))
				{
					int newSeverity = foundElem.Severity + 1;
					await UtxoReferee.UnbanAsync(prevOut).ConfigureAwait(false); // since it's not an UTXO anymore

					if (RoundConfig.DosSeverity >= newSeverity)
					{
						var txCoins = tx.Outputs.AsIndexedOutputs().Select(x => x.ToCoin().Outpoint);
						await UtxoReferee.BanUtxosAsync(newSeverity, foundElem.TimeOfBan, forceNoted: foundElem.IsNoted, foundElem.BannedForRound, txCoins.ToArray()).ConfigureAwait(false);
					}
				}
			}
		}
	}

	public async Task MakeSureInputregistrableRoundRunningAsync()
	{
		if (!Rounds.Any(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration))
		{
			int confirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: true).ConfigureAwait(false);
			var round = new CoordinatorRound(RpcClient, UtxoReferee, RoundConfig, confirmationTarget, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate, TimeSpan.FromSeconds(RoundConfig.InputRegistrationTimeout));
			round.CoinJoinBroadcasted += Round_CoinJoinBroadcasted;
			round.StatusChanged += Round_StatusChangedAsync;
			await round.ExecuteNextPhaseAsync(RoundPhase.InputRegistration).ConfigureAwait(false);
			Rounds = Rounds.Add(round);
		}
	}

	/// <summary>
	/// Depending on the number of unconfirmed coinjoins lower the confirmation target.
	/// https://github.com/zkSNACKs/WalletWasabi/issues/1155
	/// </summary>
	private async Task<int> AdjustConfirmationTargetAsync(bool lockCoinJoins)
	{
		try
		{
			int unconfirmedCoinJoinsCount = 0;
			if (lockCoinJoins)
			{
				using (await CoinJoinsLock.LockAsync().ConfigureAwait(false))
				{
					unconfirmedCoinJoinsCount = UnconfirmedCoinJoins.Count;
				}
			}
			else
			{
				unconfirmedCoinJoinsCount = UnconfirmedCoinJoins.Count;
			}

			int confirmationTarget = CoordinatorRound.AdjustConfirmationTarget(unconfirmedCoinJoinsCount, RoundConfig.ConfirmationTarget, RoundConfig.ConfirmationTargetReductionRate);
			return confirmationTarget;
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Adjusting confirmation target failed. Falling back to default, specified in config.");
			Logger.LogWarning(ex);

			return RoundConfig.ConfirmationTarget;
		}
	}

	private void Round_CoinJoinBroadcasted(object? sender, Transaction transaction)
	{
		CoinJoinBroadcasted?.Invoke(sender, transaction);
	}

	private async void Round_StatusChangedAsync(object? sender, CoordinatorRoundStatus status)
	{
		try
		{
			CoordinatorRound round = (CoordinatorRound)sender!;

			// If success save the coinjoin.
			if (status == CoordinatorRoundStatus.Succeded)
			{
				uint256[]? mempoolHashes = null;
				try
				{
					mempoolHashes = await RpcClient.GetRawMempoolAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}

				using (await CoinJoinsLock.LockAsync().ConfigureAwait(false))
				{
					uint256 coinJoinHash = round.CoinJoin.GetHash();
					lock (UnconfirmedCoinJoinsLock)
					{
						if (mempoolHashes is { })
						{
							var fallOuts = UnconfirmedCoinJoins.Where(x => !mempoolHashes.Contains(x)).ToHashSet();
							CoinJoins.RemoveAll(x => fallOuts.Contains(x));
							UnconfirmedCoinJoins.RemoveAll(x => fallOuts.Contains(x));
						}

						CoinJoins.Add(coinJoinHash);
						UnconfirmedCoinJoins.Add(coinJoinHash);
					}
					LastSuccessfulCoinJoinTime = DateTimeOffset.UtcNow;
					await File.AppendAllLinesAsync(CoinJoinsFilePath, new[] { coinJoinHash.ToString() }).ConfigureAwait(false);

					// When a round succeeded, adjust the denomination as to users still be able to register with the latest round's active output amount.
					IEnumerable<(Money value, int count)> outputs = round.CoinJoin.GetIndistinguishableOutputs(includeSingle: true);
					var bestOutput = outputs.OrderByDescending(x => x.count).FirstOrDefault();
					if (bestOutput != default)
					{
						Money activeOutputAmount = bestOutput.value;

						int currentConfirmationTarget = await AdjustConfirmationTargetAsync(lockCoinJoins: false).ConfigureAwait(false);
						var fees = await CoordinatorRound.CalculateFeesAsync(RpcClient, currentConfirmationTarget).ConfigureAwait(false);
						var feePerInputs = fees.feePerInputs;
						var feePerOutputs = fees.feePerOutputs;

						Money newDenominationToGetInWithactiveOutputs = activeOutputAmount - (feePerInputs + (2 * feePerOutputs));
						if (newDenominationToGetInWithactiveOutputs < RoundConfig.Denomination)
						{
							if (newDenominationToGetInWithactiveOutputs > Money.Coins(0.01m))
							{
								RoundConfig.Denomination = newDenominationToGetInWithactiveOutputs;
								RoundConfig.ToFile();
							}
						}
					}
				}
			}

			// If aborted in signing phase, then ban Alices that did not sign.
			if (status == CoordinatorRoundStatus.Aborted && round.Phase == RoundPhase.Signing)
			{
				IEnumerable<Alice> alicesDidntSign = round.GetAlicesByNot(AliceState.SignedCoinJoin, syncLock: false);

				if (TryGetCurrentInputRegisterableRound(out CoordinatorRound? nextRound))
				{
					int nextRoundAlicesCount = nextRound.CountAlices(syncLock: false);
					var alicesSignedCount = round.AnonymitySet - alicesDidntSign.Count();

					// New round's anonset should be the number of alices that signed in this round.
					// Except if the number of alices in the next round is already larger.
					var newAnonymitySet = Math.Max(alicesSignedCount, nextRoundAlicesCount);

					// But it cannot be larger than the current anonset of that round.
					newAnonymitySet = Math.Min(newAnonymitySet, nextRound.AnonymitySet);

					// Only change the anonymity set of the next round if new anonset does not equal and new anonset is larger than 1.
					if (nextRound.AnonymitySet != newAnonymitySet && newAnonymitySet > 1)
					{
						nextRound.UpdateAnonymitySet(newAnonymitySet, syncLock: false);

						if (nextRoundAlicesCount >= nextRound.AnonymitySet)
						{
							// Progress to the next phase, which will be OutputRegistration
							await nextRound.ExecuteNextPhaseAsync(RoundPhase.ConnectionConfirmation).ConfigureAwait(false);
						}
					}
				}

				foreach (Alice alice in alicesDidntSign) // Because the event sometimes is raised from inside the lock.
				{
					// If it is from any coinjoin, then do not ban.
					IEnumerable<OutPoint> utxosToBan = alice.Inputs.Select(x => x.Outpoint);
					await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, round.RoundId, utxosToBan.ToArray()).ConfigureAwait(false);
				}
			}

			// If finished start a new round.
			if (status is CoordinatorRoundStatus.Aborted or CoordinatorRoundStatus.Succeded)
			{
				round.StatusChanged -= Round_StatusChangedAsync;
				round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	public void AbortAllRoundsInInputRegistration(string reason, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		foreach (var r in Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration))
		{
			r.Abort(reason, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
		}
	}

	public IEnumerable<CoordinatorRound> GetRunningRounds()
	{
		return Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running).OrderBy(x => x.RemainingInputRegistrationTime).ToArray();
	}

	public bool TryGetCurrentInputRegisterableRound([NotNullWhen(true)] out CoordinatorRound? coordinatorRound)
	{
		coordinatorRound = Rounds.FirstOrDefault(x => x.Status == CoordinatorRoundStatus.Running && x.Phase == RoundPhase.InputRegistration);
		return coordinatorRound is not null;
	}

	public bool TryGetRound(long roundId, [NotNullWhen(true)] out CoordinatorRound? coordinatorRound)
	{
		coordinatorRound = Rounds.SingleOrDefault(x => x.RoundId == roundId);
		return coordinatorRound is not null;
	}

	public bool AnyRunningRoundContainsInput(OutPoint input, out List<Alice> alices)
	{
		alices = new List<Alice>();
		foreach (var round in Rounds.Where(x => x.Status == CoordinatorRoundStatus.Running))
		{
			if (round.ContainsInput(input, out List<Alice> roundAlices))
			{
				foreach (var alice in roundAlices)
				{
					alices.Add(alice);
				}
			}
		}
		return alices.Count > 0;
	}

	public int GetCoinJoinCount()
	{
		return CoinJoins.Count;
	}

	public IEnumerable<uint256> GetUnconfirmedCoinJoins()
	{
		lock (UnconfirmedCoinJoinsLock)
		{
			return UnconfirmedCoinJoins.ToArray();
		}
	}

	#region IDisposable Support

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				if (BlockNotifier is { })
				{
					BlockNotifier.OnBlock -= BlockNotifier_OnBlockAsync;
				}

				foreach (CoordinatorRound round in Rounds)
				{
					round.StatusChanged -= Round_StatusChangedAsync;
					round.CoinJoinBroadcasted -= Round_CoinJoinBroadcasted;
				}

				try
				{
					string roundCountFilePath = Path.Combine(FolderPath, "RoundCount.txt");

					IoHelpers.EnsureContainingDirectoryExists(roundCountFilePath);
					File.WriteAllText(roundCountFilePath, CoordinatorRound.RoundCount.ToString());
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			}

			_disposedValue = true;
		}
	}

	// This code added to correctly implement the disposable pattern.
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		Dispose(true);
		//// GC.SuppressFinalize(this);
	}

	#endregion IDisposable Support
}
