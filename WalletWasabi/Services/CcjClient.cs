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

		private List<(SmartCoin coin, ISecret secret, long? roundId)> CoinsToMix { get; }

		private IEnumerable<CcjRunningRoundState> _roundStates;
		public IEnumerable<CcjRunningRoundState> RoundStates
		{
			get => _roundStates;
			private set
			{
				if (_roundStates != value)
				{
					_roundStates = value;
					RoundStatesChanged?.Invoke(this, value);
				}
			}
		}
		public event EventHandler<IEnumerable<CcjRunningRoundState>> RoundStatesChanged;

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

			_roundStates = null;
			_running = 0;
			Stop = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			CoinsToMix = new List<(SmartCoin, ISecret, long?)>();
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

							var count = 0;
							using (await MixLock.LockAsync())
							{
								// if mixing >- connConf: delay = new Random().Next(2, 7);
								var notNullRoundIds = CoinsToMix.Where(x => x.roundId != null).Select(y => y.roundId).Distinct();								
								foreach (var roundId in notNullRoundIds)
								{
									count += RoundStates.Count(x => x.RoundId == roundId && x.Phase >= CcjRoundPhase.ConnectionConfirmation);
								}
							}

							if (count > 0)
							{
								var delay = new Random().Next(2, 7);
								await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);
								await ProcessStatusAsync();
							}
							else if (Interlocked.Read(ref _frequentStatusProcessingIfNotMixing) == 1 || CoinsToMix.Count != 0)
							{
								double rand = double.Parse($"0.{new Random().Next(2, 8)}"); // randomly between every 0.2 * connConfTimeout and 0.8 * connConfTimeout
								int delay = (int)(rand * RoundStates.First(x => x.Phase == CcjRoundPhase.InputRegistration).RegistrationTimeout);

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
				RoundStates = states;

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

		public void QueueCoinsToMix(string password, params SmartCoin[] coins)
		{
			using (MixLock.Lock())
			{
				foreach (SmartCoin coin in coins)
				{
					if (coin.SpentOrLocked)
					{
						continue;
					}
					var secret = KeyManager.GetSecrets(password, coin.ScriptPubKey).SingleOrDefault();
					if (secret == null)
					{
						continue;
					}
					if (CoinsToMix.Select(x => x.coin).Contains(coin))
					{
						continue;
					}

					coin.Locked = true;
					CoinsToMix.Add((coin, secret, null));
				}
			}
		}

		public async Task DequeueCoinsFromMixAsync(params (uint256 transactionId, int index)[] coins)
		{
			using (await MixLock.LockAsync())
			{
				AggregateException aggregateException = null;
				foreach (var coin in coins)
				{
					// if wasn't even queued return
					// if its round is >= connconf: add to aggregateException, continue; cannot dequeue
					// if its round is inputreg, send unconfirm req and depending on the response add to aggregateException, continue; or unqueue coin and remove roundId from its siblings (were registered to the same round)
					// unqueue coin: 1) remove from list, 2) make coin unlocked
				}

				if (aggregateException != null)
				{
					if (aggregateException.InnerExceptions.Count == 1)
					{
						throw aggregateException.InnerExceptions.First();
					}
					else
					{
						throw aggregateException;
					}
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
				await DequeueCoinsFromMixAsync(CoinsToMix.Select(x => (x.coin.TransactionId, x.coin.Index)).ToArray()); 
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjClient>("Couldn't dequeue all coins. Some coins will likely be banned from mixing.");
				Logger.LogError<CcjClient>(ex);
			}
		}
	}
}
