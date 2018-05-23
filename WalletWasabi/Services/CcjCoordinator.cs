using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class CcjCoordinator : IDisposable
	{
		private List<CcjRound> Rounds { get; }
		private AsyncLock RoundsListLock { get; }

		private List<uint256> UnconfirmedCoinJoins { get; }
		private List<uint256> CoinJoins { get; }
		public string CoinJoinsFilePath => Path.Combine(FolderPath, $"CoinJoins{Network}.txt");
		private AsyncLock CoinJoinsLock { get; }

		public RPCClient RpcClient { get; }

		public CcjRoundConfig RoundConfig { get; private set; }

		public Network Network { get; }

		public string FolderPath { get; }

		public BlindingRsaKey RsaKey { get; }

		public UtxoReferee UtxoReferee { get; }

		public CcjCoordinator(Network network, string folderPath, RPCClient rpc, CcjRoundConfig roundConfig)
		{
			Network = Guard.NotNull(nameof(network), network);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			Rounds = new List<CcjRound>();
			RoundsListLock = new AsyncLock();

			CoinJoins = new List<uint256>();
			UnconfirmedCoinJoins = new List<uint256>();
			CoinJoinsLock = new AsyncLock();

			Directory.CreateDirectory(FolderPath);

			UtxoReferee = new UtxoReferee(Network, FolderPath, RpcClient);

			// Initialize RsaKey
			string rsaKeyPath = Path.Combine(FolderPath, "RsaKey.json");
			if (File.Exists(rsaKeyPath))
			{
				string rsaKeyJson = File.ReadAllText(rsaKeyPath, encoding: Encoding.UTF8);
				RsaKey = BlindingRsaKey.CreateFromJson(rsaKeyJson);
			}
			else
			{
				RsaKey = new BlindingRsaKey();
				File.WriteAllText(rsaKeyPath, RsaKey.ToJson(), encoding: Encoding.UTF8);
				Logger.LogInfo<CcjCoordinator>($"Created RSA key at: {rsaKeyPath}");
			}

			if (File.Exists(CoinJoinsFilePath))
			{
				try
				{
					var toRemove = new List<string>();
					string[] allLines = File.ReadAllLines(CoinJoinsFilePath);
					foreach (string line in allLines)
					{
						try
						{
							uint256 txHash = new uint256(line);
							RPCResponse getRawTransactionResponse = RpcClient.SendCommand(RPCOperations.getrawtransaction, txHash.ToString(), true);
							CoinJoins.Add(txHash);
							if (getRawTransactionResponse.Result.Value<int>("confirmations") <= 0)
							{
								UnconfirmedCoinJoins.Add(txHash);
							}
						}
						catch (Exception ex)
						{
							toRemove.Add(line);

							var logEntry = ex is RPCException rpce && rpce.RPCCode == RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY
								? $"CoinJoins file contains invalid transaction ID {line}"
								: $"CoinJoins file got corrupted. Deleting offending line \"{line.Substring(0, 20)}\".";

							Logger.LogWarning<CcjCoordinator>($"{logEntry}. {ex.GetType()}: {ex.Message}");
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
					Logger.LogWarning<CcjCoordinator>($"CoinJoins file got corrupted. Deleting {CoinJoinsFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(CoinJoinsFilePath);
				}
			}
		}

		public async Task ProcessBlockAsync(Block block)
		{
			// https://github.com/zkSNACKs/WalletWasabi/issues/145
			// whenever a block arrives:
			//    go through all its transactions
			//       if a transaction spends a banned output AND it's not CJ output
			//          ban all the outputs of the transaction

			foreach (Transaction tx in block.Transactions)
			{
				if (RoundConfig.DosSeverity <= 1) return;
				var txId = tx.GetHash();

				foreach (TxIn input in tx.Inputs)
				{
					OutPoint prevOut = input.PrevOut;

					var found = UtxoReferee.BannedUtxos.SingleOrDefault(x => x.Key == prevOut);
					if (found.Key == default) continue; // if coin is not banned

					if (!AnyRunningRoundContainsInput(prevOut, out _))
					{
						int newSeverity = found.Value.severity + 1;
						await UtxoReferee.UnbanAsync(prevOut); // since it's not an UTXO anymore

						if (RoundConfig.DosSeverity >= newSeverity)
						{
							var txCoins = tx.Outputs.AsIndexedOutputs().Select(x => x.ToCoin().Outpoint);
							await UtxoReferee.BanUtxosAsync(newSeverity, found.Value.timeOfBan, txCoins.ToArray());
						}
					}
				}
			}
		}

		public void UpdateRoundConfig(CcjRoundConfig roundConfig)
		{
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
		}

		public async Task MakeSureTwoRunningRoundsAsync()
		{
			using (await RoundsListLock.LockAsync())
			{
				int runningRoundCount = Rounds.Count(x => x.Status == CcjRoundStatus.Running);
				if (runningRoundCount == 0)
				{
					var round = new CcjRound(RpcClient, UtxoReferee, RoundConfig);
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round);

					var round2 = new CcjRound(RpcClient, UtxoReferee, RoundConfig);
					round2.StatusChanged += Round_StatusChangedAsync;
					await round2.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round2);
				}
				else if (runningRoundCount == 1)
				{
					var round = new CcjRound(RpcClient, UtxoReferee, RoundConfig);
					round.StatusChanged += Round_StatusChangedAsync;
					await round.ExecuteNextPhaseAsync(CcjRoundPhase.InputRegistration);
					Rounds.Add(round);
				}
			}
		}

		private async void Round_StatusChangedAsync(object sender, CcjRoundStatus status)
		{
			var round = sender as CcjRound;

			// If success save the coinjoin.
			if (status == CcjRoundStatus.Succeded)
			{
				using (await CoinJoinsLock.LockAsync())
				{
					uint256 coinJoinHash = round.SignedCoinJoin.GetHash();
					CoinJoins.Add(coinJoinHash);
					await File.AppendAllLinesAsync(CoinJoinsFilePath, new[] { coinJoinHash.ToString() });
				}
			}

			// If failed in signing phase, then ban Alices those didn't sign.
			if (status == CcjRoundStatus.Failed && round.Phase == CcjRoundPhase.Signing)
			{
				foreach (Alice alice in round.GetAlicesByNot(AliceState.SignedCoinJoin, syncLock: false)) // Because the event sometimes is raised from inside the lock.
				{
					// If its from any coinjoin, then don't ban.
					IEnumerable<OutPoint> utxosToBan = alice.Inputs.Select(x => x.OutPoint);
					await UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, utxosToBan.ToArray());
				}
			}

			// If finished start a new round.
			if (status == CcjRoundStatus.Failed || status == CcjRoundStatus.Succeded)
			{
				round.StatusChanged -= Round_StatusChangedAsync;
				await MakeSureTwoRunningRoundsAsync();
			}
		}

		public void FailAllRoundsInInputRegistration()
		{
			using (RoundsListLock.Lock())
			{
				foreach (var r in Rounds.Where(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration))
				{
					r.Fail();
				}
			}
		}

		public IEnumerable<CcjRound> GetRunningRounds()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.Where(x => x.Status == CcjRoundStatus.Running).ToArray();
			}
		}

		public CcjRound GetCurrentInputRegisterableRound()
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.First(x => x.Status == CcjRoundStatus.Running && x.Phase == CcjRoundPhase.InputRegistration); // not FirstOrDefault, it must always exist
			}
		}

		public CcjRound TryGetRound(long roundId)
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.SingleOrDefault(x => x.RoundId == roundId);
			}
		}

		public CcjRound TryGetRound(string roundHash)
		{
			using (RoundsListLock.Lock())
			{
				return Rounds.SingleOrDefault(x => x.RoundHash == roundHash);
			}
		}

		public bool AnyRunningRoundContainsInput(OutPoint input, out List<Alice> alices)
		{
			using (RoundsListLock.Lock())
			{
				alices = new List<Alice>();
				foreach (var round in Rounds.Where(x => x.Status == CcjRoundStatus.Running))
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
		}

		public bool ContainsCoinJoin(uint256 hash)
		{
			using (CoinJoinsLock.Lock())
			{
				return CoinJoins.Contains(hash);
			}
		}

		public async Task<bool> IsUnconfirmedCoinJoinLimitReachedAsync()
		{
			using (await CoinJoinsLock.LockAsync())
			{
				if (UnconfirmedCoinJoins.Count() < 24)
				{
					return false;
				}
				foreach (var cjHash in UnconfirmedCoinJoins.ToArray())
				{
					try
					{
						var txInfo = await RpcClient.GetRawTransactionInfoAsync(cjHash);

						// if confirmed remove only from unconfirmed
						if (txInfo.Confirmations > 0)
						{
							UnconfirmedCoinJoins.Remove(cjHash);
						}
					}
					catch (Exception ex)
					{
						// if failed remove from everywhere (should not happen normally)
						UnconfirmedCoinJoins.Remove(cjHash);
						CoinJoins.Remove(cjHash);
						await File.WriteAllLinesAsync(CoinJoinsFilePath, CoinJoins.Select(x => x.ToString()));
						Logger.LogWarning<CcjCoordinator>(ex);
					}
				}

				return UnconfirmedCoinJoins.Count >= 24;
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					using (RoundsListLock.Lock())
					{
						foreach (CcjRound round in Rounds)
						{
							round.StatusChanged -= Round_StatusChangedAsync;
						}
					}
				}

				_disposedValue = true;
			}
		}

		// ~CcjCoordinator() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
