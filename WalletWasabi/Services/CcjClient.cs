using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class CcjClient
	{
		public Network Network { get; }
		public KeyManager KeyManager { get; }

		public AliceClient AliceClient { get; }
		public BobClient BobClient { get; }
		public SatoshiClient SatoshiClient { get; }

		private AsyncLock MixLock { get; }

		private List<MixCoin> CoinsToMix { get; }

		private List<CcjClientRound> Rounds { get; }
		public event EventHandler<CcjClientRound> RoundAdded;
		public event EventHandler<long> RoundRemoved;
		public event EventHandler<CcjClientRound> RoundUpdated;

		private long _frequentStatusProcessingIfNotMixing;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Stop { get; }

		public CcjClient(Network network, KeyManager keyManager, Uri ccjHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			AliceClient = new AliceClient(ccjHostUri, torSocks5EndPoint);
			BobClient = new BobClient(ccjHostUri, torSocks5EndPoint);
			SatoshiClient = new SatoshiClient(ccjHostUri, torSocks5EndPoint);

			Rounds = new List<CcjClientRound>();
			_running = 0;
			Stop = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			CoinsToMix = new List<MixCoin>();
			MixLock = new AsyncLock();
		}

		public void Start()
		{
			Interlocked.Exchange(ref _running, 1);

			// At start the client asks for status.

			// The client is asking for status periodically, randomly between every 0.2 * connConfTimeout and 0.8 * connConfTimeout.
			// - if the GUI is at the mixer tab -Activate(), DeactivateIfNotMixing().
			// - if coins are queued to mix.
			// The client is asking for status periodically, randomly between every 2 to 7 seconds.
			// - if it is participating in a mix thats status >= connConf.

			// The client is triggered only when a status response arrives. The answer to the server is delayed randomly from 0 to 7 seconds.

			Task.Run(async () =>
			{
				try
				{
					await ProcessStatusAsync();

					while (IsRunning)
					{
						try
						{
							// If stop was requested return.
							if (IsRunning == false) return;

							var inputRegMixing = false;
							var activelyMixing = false;
							using (await MixLock.LockAsync())
							{
								// if mixing >= connConf: delay = new Random().Next(2, 7);
								activelyMixing = Rounds.Any(x => x.AliceUniqueId != null && x.State.Phase >= CcjRoundPhase.ConnectionConfirmation);
								inputRegMixing = Rounds.Any(x => x.AliceUniqueId != null);
							}

							if (activelyMixing)
							{
								var delay = new Random().Next(2, 7);
								await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);
								await ProcessStatusAsync();
							}
							else if (Interlocked.Read(ref _frequentStatusProcessingIfNotMixing) == 1 || inputRegMixing)
							{
								double rand = double.Parse($"0.{new Random().Next(2, 8)}"); // randomly between every 0.2 * connConfTimeout and 0.8 * connConfTimeout
								int delay;
								using (await MixLock.LockAsync())
								{
									delay = (int)(rand * Rounds.First(x => x.State.Phase == CcjRoundPhase.InputRegistration).State.RegistrationTimeout);
								}

								await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);
								await ProcessStatusAsync();
							}
							else
							{
								await Task.Delay(1000); // dormant
							}
						}
						catch (TaskCanceledException ex)
						{
							Logger.LogTrace<CcjClient>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogError<CcjClient>(ex);
						}
					}
				}
				finally
				{
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			});
		}

		private async Task ProcessStatusAsync()
		{
			try
			{
				IEnumerable<CcjRunningRoundState> states = await SatoshiClient.GetAllRoundStatesAsync();
				using (await MixLock.LockAsync())
				{
					foreach (CcjRunningRoundState state in states)
					{
						CcjClientRound round = Rounds.SingleOrDefault(x => x.State.RoundId == state.RoundId);
						if (round == null) // It's a new running round.
						{
							var r = new CcjClientRound(state);
							Rounds.Add(r);
							RoundAdded?.Invoke(this, r);
						}
						else
						{
							round.State = state;
							RoundUpdated?.Invoke(this, round);
						}
					}

					var roundsToRemove = new List<long>();
					foreach (CcjClientRound round in Rounds)
					{
						CcjRunningRoundState state = states.SingleOrDefault(x => x.RoundId == round.State.RoundId);
						if (state == null) // The round is not running anymore.
						{
							roundsToRemove.Add(round.State.RoundId);
						}
					}

					foreach (long roundId in roundsToRemove)
					{
						Rounds.RemoveAll(x => x.State.RoundId == roundId);
						foreach (var coin in CoinsToMix.Where(x => x.RoundId == roundId))
						{
							coin.RemoveFromMix();
						}
						RoundRemoved?.Invoke(this, roundId);
					}
				}

				int delay = new Random().Next(0, 7); // delay the response to defend timing attack privacy
				await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);

				using (await MixLock.LockAsync())
				{

				}
			}
			catch (TaskCanceledException ex)
			{
				Logger.LogTrace<CcjClient>(ex);
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjClient>(ex);
			}
		}

		public void ActivateFrequentStatusProcessing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 1);
		}

		public void DeactivateFrequentStatusProcessingIfNotMixing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 0);
		}

		/// <summary>
		/// Lock the coins before queueing them.
		/// </summary>
		public IEnumerable<SmartCoin> QueueCoinsToMix(string password, params SmartCoin[] coins)
		{
			using (MixLock.Lock())
			{
				var successful = new List<SmartCoin>();

				foreach (SmartCoin coin in coins)
				{
					if (coin.SpenderTransactionId != null)
					{
						continue;
					}
					var secret = KeyManager.GetSecrets(password, coin.ScriptPubKey).SingleOrDefault();
					if (secret == null)
					{
						continue;
					}
					if (CoinsToMix.Select(x => x.SmartCoin).Contains(coin))
					{
						continue;
					}

					CoinsToMix.Add(new MixCoin(coin, secret));
					successful.Add(coin);
				}

				return successful;
			}
		}

		/// <summary>
		/// Unlock coin after dequeuing it.
		/// </summary>
		public async Task DequeueCoinsFromMixAsync(params (uint256 transactionId, int index)[] coins)
		{
			using (await MixLock.LockAsync())
			{
				List<Exception> exceptions = new List<Exception>();

				foreach (var coinToDequeue in coins)
				{
					MixCoin coinToMix = CoinsToMix.SingleOrDefault(x => x.SmartCoin.TransactionId == coinToDequeue.transactionId && x.SmartCoin.Index == coinToDequeue.index);
					// if wasn't even queued continue
					if (coinToMix == null)
					{
						continue;
					}
					// if its round is >= connconf: add to aggregateException, continue; cannot dequeue
					if (coinToMix.RoundId != null)
					{
						CcjClientRound round = Rounds.Single(x => x.State.RoundId == coinToMix.RoundId); // Round must be present and running.
						if (round.State.Phase >= CcjRoundPhase.ConnectionConfirmation)
						{
							exceptions.Add(new NotSupportedException($"Cannot deque coin in {round.State.Phase} phase. Coin: {coinToDequeue.index}:{coinToDequeue.transactionId}."));
							continue;
						}
						else // // if its round is inputreg, send unconfirm req and depending on the response add to aggregateException, continue; or unqueue coin and remove roundId from its siblings (were registered to the same round)
						{
							try
							{
								await AliceClient.PostUnConfirmationAsync(round.State.RoundId, (Guid)round.AliceUniqueId); // AliceUniqueId must be there.
								foreach(var c in CoinsToMix)
								{
									if(c.RoundId == round.State.RoundId)
									{
										c.RemoveFromMix();
									}
								}
							}
							catch (Exception ex)
							{
								exceptions.Add(ex);
								continue;
							}
						}
					}
					coinToMix.RemoveFromMix();
					CoinsToMix.Remove(coinToMix);
				}

				if (exceptions.Count == 1)
				{
					throw exceptions.Single();
				}
				else if (exceptions.Count > 0)
				{
					throw new AggregateException(exceptions);
				}
			}
		}

		public async Task StopAsync()
		{
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			Stop?.Cancel();
			while (IsStopping)
			{
				await Task.Delay(50);
			}

			Stop?.Dispose();
			SatoshiClient?.Dispose();
			BobClient?.Dispose();
			AliceClient?.Dispose();

			try
			{
				// Try to dequeue all coins at last. This should be done manually, and prevent the closing of the software if unsuccessful.
				await DequeueCoinsFromMixAsync(CoinsToMix.Select(x => (x.SmartCoin.TransactionId, x.SmartCoin.Index)).ToArray()); 
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjClient>("Couldn't dequeue all coins. Some coins will likely be banned from mixing.");
				Logger.LogError<CcjClient>(ex);
			}
		}
	}
}
