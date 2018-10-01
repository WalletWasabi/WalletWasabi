using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using System.Collections.Concurrent;

namespace WalletWasabi.Services
{
	public class CcjClient
	{
		public Network Network { get; }
		private BlindingRsaPubKey CoordinatorPubKey { get; }
		public KeyManager KeyManager { get; }

		public List<BitcoinAddress> CustomChangeAddresses { get; }
		private object CustomChangeAddressesLock { get; }
		public List<BitcoinAddress> CustomActiveAddresses { get; }
		private object CustomActiveAddressesLock { get; }

		public SatoshiClient SatoshiClient { get; }
		public Uri CcjHostUri { get; }
		private IPEndPoint TorSocks5EndPoint { get; }

		/// <summary>
		/// Used to avoid address reuse as much as possible but still not bloating the wallet.
		/// </summary>
		private ConcurrentDictionary<HdPubKey, DateTimeOffset> AccessCache { get; }

		private AsyncLock MixLock { get; }

		public CcjClientState State { get; }

		public event EventHandler StateUpdated;

		public event EventHandler<SmartCoin> CoinQueued;

		public event EventHandler<SmartCoin> CoinDequeued;

		private long _frequentStatusProcessingIfNotMixing;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Cancel { get; }

		public CcjClient(Network network, BlindingRsaPubKey coordinatorPubKey, KeyManager keyManager, Uri ccjHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			AccessCache = new ConcurrentDictionary<HdPubKey, DateTimeOffset>();
			Network = Guard.NotNull(nameof(network), network);
			CoordinatorPubKey = Guard.NotNull(nameof(coordinatorPubKey), coordinatorPubKey);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			CcjHostUri = Guard.NotNull(nameof(ccjHostUri), ccjHostUri);
			TorSocks5EndPoint = torSocks5EndPoint;
			SatoshiClient = new SatoshiClient(ccjHostUri, torSocks5EndPoint);

			_running = 0;
			Cancel = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			State = new CcjClientState();
			MixLock = new AsyncLock();

			CustomChangeAddresses = new List<BitcoinAddress>();
			CustomActiveAddresses = new List<BitcoinAddress>();
			CustomChangeAddressesLock = new object();
			CustomActiveAddressesLock = new object();
		}

		public void Start(int minDelayReplySeconds, int maxDelayReplySeconds)
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
					await ProcessStatusAsync(minDelayReplySeconds, maxDelayReplySeconds);

					Logger.LogInfo<CcjClient>("CcjClient is successfully initialized.");

					while (IsRunning)
					{
						try
						{
							int delaySeconds;
							using (await MixLock.LockAsync())
							{
								await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

								// If stop was requested return.
								if (IsRunning == false) return;

								// if mixing >= connConf: delay = new Random().Next(1, 3);
								if (State.GetActivelyMixingRounds().Any())
								{
									delaySeconds = new Random().Next(1, 3);
								}
								else if (Interlocked.Read(ref _frequentStatusProcessingIfNotMixing) == 1 || State.GetPassivelyMixingRounds().Any() || State.GetWaitingListCount() > 0)
								{
									double rand = double.Parse($"0.{new Random().Next(1, 6)}"); // randomly between every 0.1 * connConfTimeout - 7 and 0.6 * connConfTimeout
									delaySeconds = Math.Max(0, (int)(rand * State.GetSmallestRegistrationTimeout() - 7));
								}
								else
								{
									// dormant
									await Task.Delay(1000); // dormant
									continue;
								}
							}

							await Task.Delay(TimeSpan.FromSeconds(delaySeconds), Cancel.Token);
							await ProcessStatusAsync(minDelayReplySeconds, maxDelayReplySeconds);
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

		private async Task ProcessStatusAsync(int minDelayReplySeconds, int maxDelayReplySeconds)
		{
			try
			{
				IEnumerable<CcjRunningRoundState> states;
				int delay;
				using (await MixLock.LockAsync())
				{
					await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

					states = await SatoshiClient.GetAllRoundStatesAsync();
					State.UpdateRoundsByStates(states.ToArray());

					// If we don't have enough coin queued to register a round, then dequeue all.
					CcjClientRound registrableRound = State.GetRegistrableRoundOrDefault();
					if (registrableRound != default)
					{
						if (!registrableRound.State.HaveEnoughQueued(State.GetAllQueuedCoinAmounts().ToArray()))
						{
							await DequeueAllCoinsFromMixNoLockAsync();
						}
					}

					StateUpdated?.Invoke(this, null);
					if (maxDelayReplySeconds == minDelayReplySeconds)
					{
						delay = minDelayReplySeconds;
					}
					if (maxDelayReplySeconds < minDelayReplySeconds || maxDelayReplySeconds <= 0)
					{
						delay = 0;
					}
					else
					{
						delay = new Random().Next(minDelayReplySeconds, maxDelayReplySeconds); // delay the response to defend timing attack privacy
					}
				}

				await Task.Delay(TimeSpan.FromSeconds(delay), Cancel.Token);

				using (await MixLock.LockAsync())
				{
					foreach (long ongoingRoundId in State.GetActivelyMixingRounds())
					{
						await TryProcessRoundStateAsync(ongoingRoundId);
					}

					await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

					CcjClientRound inputRegistrableRound = State.GetRegistrableRoundOrDefault();
					if (!(inputRegistrableRound is null))
					{
						if (inputRegistrableRound.AliceClient is null) // If didn't register already, check what can we register.
						{
							await TryRegisterCoinsAsync(inputRegistrableRound);
						}
						else // We registered, let's confirm we're online.
						{
							await TryConfirmConnectionAsync(inputRegistrableRound);
						}
					}
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

		private async Task TryProcessRoundStateAsync(long ongoingRoundId)
		{
			try
			{
				var ongoingRound = State.GetSingleOrDefaultRound(ongoingRoundId);
				if (ongoingRound is null) throw new NotSupportedException("This is impossible.");

				if (ongoingRound.State.Phase == CcjRoundPhase.ConnectionConfirmation)
				{
					if (ongoingRound.RoundHash is null) // If we didn't already obtained our roundHash obtain it.
					{
						await ObtainRoundHashAsync(ongoingRound);
					}
				}
				else if (ongoingRound.State.Phase == CcjRoundPhase.OutputRegistration)
				{
					if (!ongoingRound.PostedOutput)
					{
						if (ongoingRound.RoundHash is null)
						{
							throw new NotSupportedException("Coordinator progressed to OutputRegistration phase, even though we didn't obtain roundHash.");
						}

						await RegisterOutputAsync(ongoingRound);
					}
				}
				else if (ongoingRound.State.Phase == CcjRoundPhase.Signing)
				{
					if (!ongoingRound.Signed)
					{
						Transaction unsignedCoinJoin = await ongoingRound.AliceClient.GetUnsignedCoinJoinAsync();
						Dictionary<int, WitScript> myDic = SignCoinJoin(ongoingRound, unsignedCoinJoin);

						await ongoingRound.AliceClient.PostSignaturesAsync(myDic);
						ongoingRound.Signed = true;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjClient>(ex); // Keep this in front of the logic (Logs will make more sense.)
				if (ex.Message.StartsWith("Not Found", StringComparison.Ordinal)) // Alice timed out.
				{
					State.ClearRoundRegistration(ongoingRoundId);
				}
			}
		}

		private Dictionary<int, WitScript> SignCoinJoin(CcjClientRound ongoingRound, Transaction unsignedCoinJoin)
		{
			if (NBitcoinHelpers.HashOutpoints(unsignedCoinJoin.Inputs.Select(x => x.PrevOut)) != ongoingRound.RoundHash)
			{
				throw new NotSupportedException("Coordinator provided invalid roundHash.");
			}
			Money amountBack = unsignedCoinJoin.Outputs
				.Where(x => x.ScriptPubKey == ongoingRound.ActiveOutputAddress.ScriptPubKey || x.ScriptPubKey == ongoingRound.ChangeOutputAddress.ScriptPubKey)
				.Select(y => y.Value)
				.Sum();
			Money minAmountBack = ongoingRound.CoinsRegistered.Sum(x => x.Amount); // Start with input sum.
			minAmountBack -= ongoingRound.State.FeePerOutputs * 2 + ongoingRound.State.FeePerInputs * ongoingRound.CoinsRegistered.Count; // Minus miner fee.
			Money actualDenomination = unsignedCoinJoin.GetIndistinguishableOutputs().OrderByDescending(x => x.count).First().value; // Denomination may grow.
			Money expectedCoordinatorFee = actualDenomination.Percentange(ongoingRound.State.CoordinatorFeePercent) * ongoingRound.State.RequiredPeerCount;
			minAmountBack -= expectedCoordinatorFee; // Minus expected coordinator fee.

			// If there's no change output then coordinator protection may happened:
			if (unsignedCoinJoin.Outputs.All(x => x.ScriptPubKey != ongoingRound.ChangeOutputAddress.ScriptPubKey))
			{
				Money minimumOutputAmount = Money.Coins(0.0001m); // If the change would be less than about $1 then add it to the coordinator.
				Money onePercentOfDenomination = actualDenomination.Percentange(1m); // If the change is less than about 1% of the newDenomination then add it to the coordinator fee.
				Money minimumChangeAmount = Math.Max(minimumOutputAmount, onePercentOfDenomination);

				minAmountBack -= minimumChangeAmount; // Minus coordinator protections (so it won't create bad coinjoins.)
			}

			if (amountBack < minAmountBack)
			{
				throw new NotSupportedException("Coordinator did not add enough value to our outputs in the coinjoin.");
			}

			var builder = new TransactionBuilder();
			var signedCoinJoin = builder
				.ContinueToBuild(unsignedCoinJoin)
				.AddKeys(ongoingRound.CoinsRegistered.Select(x => x.Secret = x.Secret ?? KeyManager.GetSecrets(OnePiece, x.ScriptPubKey).Single()).ToArray())
				.AddCoins(ongoingRound.CoinsRegistered.Select(x => x.GetCoin()))
				.BuildTransaction(true);

			var myDic = new Dictionary<int, WitScript>();

			for (int i = 0; i < signedCoinJoin.Inputs.Count; i++)
			{
				var input = signedCoinJoin.Inputs[i];
				if (ongoingRound.CoinsRegistered.Select(x => x.GetOutPoint()).Contains(input.PrevOut))
				{
					myDic.Add(i, signedCoinJoin.Inputs[i].WitScript);
				}
			}

			return myDic;
		}

		private async Task RegisterOutputAsync(CcjClientRound ongoingRound)
		{
			using (var bobClient = new BobClient(CcjHostUri, TorSocks5EndPoint))
			{
				await bobClient.PostOutputAsync(ongoingRound.RoundHash, ongoingRound.ActiveOutputAddress, ongoingRound.UnblindedSignature);
				ongoingRound.PostedOutput = true;
				Logger.LogInfo<AliceClient>($"Round ({ongoingRound.State.RoundId}) Bob Posted output.");
			}
		}

		private static async Task ObtainRoundHashAsync(CcjClientRound ongoingRound)
		{
			string roundHash = await ongoingRound.AliceClient.PostConfirmationAsync();
			ongoingRound.RoundHash = roundHash ?? throw new NotSupportedException($"Coordinator didn't gave us the expected {nameof(roundHash)}, even though it's in ConnectionConfirmation phase.");
		}

		private async Task TryConfirmConnectionAsync(CcjClientRound inputRegistrableRound)
		{
			try
			{
				string roundHash = await inputRegistrableRound.AliceClient.PostConfirmationAsync();
				if (!(roundHash is null)) // Then the phase went to connection confirmation.
				{
					inputRegistrableRound.RoundHash = roundHash;
					inputRegistrableRound.State.Phase = CcjRoundPhase.ConnectionConfirmation;
				}
			}
			catch (Exception ex)
			{
				if (ex.Message.StartsWith("Not Found", StringComparison.Ordinal)) // Alice timed out.
				{
					State.ClearRoundRegistration(inputRegistrableRound.State.RoundId);
				}
				Logger.LogError<CcjClient>(ex);
			}
		}

		private async Task TryRegisterCoinsAsync(CcjClientRound inputRegistrableRound)
		{
			try
			{
				List<(uint256 txid, uint index)> registrableCoins = State.GetRegistrableCoins(
					inputRegistrableRound.State.MaximumInputCountPerPeer,
					inputRegistrableRound.State.Denomination,
					inputRegistrableRound.State.FeePerInputs,
					inputRegistrableRound.State.FeePerOutputs).ToList();

				if (registrableCoins.Any())
				{
					BitcoinAddress changeAddress = null;
					BitcoinAddress activeAddress = null;
					lock (CustomChangeAddressesLock)
					{
						if (CustomChangeAddresses.Count > 0)
						{
							changeAddress = CustomChangeAddresses.First();
							CustomChangeAddresses.RemoveFirst();
						}
					}
					lock (CustomActiveAddressesLock)
					{
						if (CustomActiveAddresses.Count > 0)
						{
							activeAddress = CustomActiveAddresses.First();
							CustomActiveAddresses.RemoveFirst();
						}
					}

					if (changeAddress is null || activeAddress is null)
					{
						IEnumerable<HdPubKey> allUnusedInternalKeys = KeyManager.GetKeys(keyState: null, isInternal: true).Where(x => x.KeyState != KeyState.Used);

						if (changeAddress is null)
						{
							string changeLabel = "ZeroLink Change";
							IEnumerable<HdPubKey> allChangeKeys = allUnusedInternalKeys.Where(x => x.Label == changeLabel);
							HdPubKey changeKey = null;

							KeyManager.AssertLockedInternalKeysIndexed(14);
							IEnumerable<HdPubKey> internalNotCachedLockedKeys = KeyManager.GetKeys(KeyState.Locked, isInternal: true).Except(AccessCache.Keys);

							if (allChangeKeys.Count() >= 7 || !internalNotCachedLockedKeys.Any()) // Then don't generate new keys, because it'd bloat the wallet.
							{
								// Find the first one that we did not try to register in the current session.
								changeKey = allChangeKeys.FirstOrDefault(x => !AccessCache.ContainsKey(x));
								// If there is no such a key, then use the oldest.
								if (changeKey == default)
								{
									changeKey = AccessCache.Where(x => allChangeKeys.Contains(x.Key)).OrderBy(x => x.Value).First().Key;
								}
								changeKey.SetLabel(changeLabel);
								changeKey.SetKeyState(KeyState.Locked);
							}
							else
							{
								changeKey = internalNotCachedLockedKeys.RandomElement();
								changeKey.SetLabel(changeLabel);
							}
							changeAddress = changeKey.GetP2wpkhAddress(Network);
							AccessCache.AddOrReplace(changeKey, DateTimeOffset.UtcNow);
						}

						if (activeAddress is null)
						{
							string activeLabel = "ZeroLink Mixed Coin";
							IEnumerable<HdPubKey> allActiveKeys = allUnusedInternalKeys.Where(x => x.Label == activeLabel);
							HdPubKey activeKey = null;

							KeyManager.AssertLockedInternalKeysIndexed(14);
							IEnumerable<HdPubKey> internalNotCachedLockedKeys = KeyManager.GetKeys(KeyState.Locked, isInternal: true).Except(AccessCache.Keys);

							if (allActiveKeys.Count() >= 7 || !internalNotCachedLockedKeys.Any()) // Then don't generate new keys, because it'd bloat the wallet.
							{
								// Find the first one that we did not try to register in the current session.
								activeKey = allActiveKeys.FirstOrDefault(x => !AccessCache.ContainsKey(x));
								// If there is no such a key, then use the oldest.
								if (activeKey == default)
								{
									activeKey = AccessCache.Where(x => allActiveKeys.Contains(x.Key)).OrderBy(x => x.Value).First().Key;
								}
								activeKey.SetLabel(activeLabel);
								activeKey.SetKeyState(KeyState.Locked);
								activeAddress = activeKey.GetP2wpkhAddress(Network);
							}
							else
							{
								activeKey = internalNotCachedLockedKeys.RandomElement();
								activeKey.SetLabel(activeLabel);
							}
							activeAddress = activeKey.GetP2wpkhAddress(Network);
							AccessCache.AddOrReplace(activeKey, DateTimeOffset.UtcNow);
						}
					}

					KeyManager.ToFile();

					var blind = CoordinatorPubKey.Blind(activeAddress.ScriptPubKey.ToBytes());

					var inputProofs = new List<InputProofModel>();
					foreach ((uint256 txid, uint index) coinReference in registrableCoins)
					{
						SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
						if (coin is null) throw new NotSupportedException("This is impossible.");
						coin.Secret = coin.Secret ?? KeyManager.GetSecrets(OnePiece, coin.ScriptPubKey).Single();
						var inputProof = new InputProofModel
						{
							Input = coin.GetTxoRef(),
							Proof = coin.Secret.PrivateKey.SignMessage(ByteHelpers.ToHex(blind.BlindedData))
						};
						inputProofs.Add(inputProof);
					}
					AliceClient aliceClient = await AliceClient.CreateNewAsync(Network, changeAddress, blind.BlindedData, inputProofs, CcjHostUri, TorSocks5EndPoint);

					byte[] unblindedSignature = CoordinatorPubKey.UnblindSignature(aliceClient.BlindedOutputSignature, blind.BlindingFactor);

					if (!CoordinatorPubKey.Verify(unblindedSignature, activeAddress.ScriptPubKey.ToBytes()))
					{
						throw new NotSupportedException("Coordinator did not sign the blinded output properly.");
					}

					CcjClientRound roundRegistered = State.GetSingleOrDefaultRound(aliceClient.RoundId);
					if (roundRegistered is null)
					{
						// If our SatoshiClient doesn't yet know about the round because of the dealy create it.
						// Make its state as it'd be the same as our assumed round was, except the roundId and registeredPeerCount, it'll be updated later.
						roundRegistered = new CcjClientRound(CcjRunningRoundState.CloneExcept(inputRegistrableRound.State, aliceClient.RoundId, registeredPeerCount: 1));
						State.AddOrReplaceRound(roundRegistered);
					}

					foreach ((uint256 txid, uint index) coinReference in registrableCoins)
					{
						var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
						if (coin is null) throw new NotSupportedException("This is impossible.");
						roundRegistered.CoinsRegistered.Add(coin);
						State.RemoveCoinFromWaitingList(coin);
					}
					roundRegistered.ActiveOutputAddress = activeAddress;
					roundRegistered.ChangeOutputAddress = changeAddress;
					roundRegistered.UnblindedSignature = unblindedSignature;
					roundRegistered.AliceClient = aliceClient;
				}
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

		#region CustomAddressesLists

		public IEnumerable<BitcoinAddress> GetCustomChangeAddresses()
		{
			lock (CustomChangeAddressesLock)
			{
				return CustomChangeAddresses;
			}
		}

		public IEnumerable<BitcoinAddress> GetCustomActiveAddresses()
		{
			lock (CustomActiveAddressesLock)
			{
				return CustomActiveAddresses;
			}
		}

		/// <summary>
		/// Best effort. For example if a round is disrupted my malicious actors, the address won't be registered again, therefore it's not guaranteed money will arrive.
		/// </summary>
		public void AddCustomActiveAddress(BitcoinAddress address, bool beginning = false)
		{
			lock (CustomActiveAddressesLock)
			{
				if (CustomActiveAddresses.Contains(address))
				{
					CustomActiveAddresses.Remove(address);
				}
				if (beginning)
				{
					CustomActiveAddresses.Insert(0, address);
				}
				else
				{
					CustomActiveAddresses.Add(address);
				}
			}
		}

		/// <summary>
		/// Best effort. For example if a round is disrupted my malicious actors, the address won't be registered again, therefore it's not guaranteed money will arrive.
		/// </summary>
		public void AddCustomChangeAddress(BitcoinAddress address, bool beginning = false)
		{
			lock (CustomChangeAddressesLock)
			{
				if (CustomChangeAddresses.Contains(address))
				{
					CustomChangeAddresses.Remove(address);
				}
				if (beginning)
				{
					CustomChangeAddresses.Insert(0, address);
				}
				else
				{
					CustomChangeAddresses.Add(address);
				}
			}
		}

		public void RemoveCustomChangeAddress(BitcoinAddress address)
		{
			lock (CustomChangeAddressesLock)
			{
				if (CustomChangeAddresses.Contains(address))
				{
					CustomChangeAddresses.Remove(address);
				}
			}
		}

		public void RemoveCustomActiveAddress(BitcoinAddress address)
		{
			lock (CustomActiveAddressesLock)
			{
				if (CustomActiveAddresses.Contains(address))
				{
					CustomActiveAddresses.Remove(address);
				}
			}
		}

		#endregion CustomAddressesLists

		internal string OnePiece { get; private set; } = null;

		public async Task<IEnumerable<SmartCoin>> QueueCoinsToMixAsync(string password, params SmartCoin[] coins)
		{
			using (await MixLock.LockAsync())
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

				var successful = new List<SmartCoin>();

				foreach (SmartCoin coin in coins)
				{
					if (State.Contains(coin))
					{
						successful.Add(coin);
						continue;
					}

					if (coin.SpentOrCoinJoinInProgress)
					{
						continue;
					}

					coin.Secret = KeyManager.GetSecrets(password, coin.ScriptPubKey).Single();
					OnePiece = OnePiece ?? password;

					coin.CoinJoinInProgress = true;

					State.AddCoinToWaitingList(coin);
					successful.Add(coin);
					CoinQueued?.Invoke(this, coin);
					Logger.LogInfo<CcjClient>($"Coin queued: {coin.Index}:{coin.TransactionId}.");
				}

				return successful;
			}
		}

		public async Task DequeueCoinsFromMixAsync(params SmartCoin[] coins)
		{
			using (await MixLock.LockAsync())
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

				await DequeueCoinsFromMixNoLockAsync(coins.Select(x => (x.TransactionId, x.Index)).ToArray());
			}
		}

		public async Task DequeueCoinsFromMixAsync(params (uint256 txid, uint index)[] coins)
		{
			using (await MixLock.LockAsync())
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

				await DequeueCoinsFromMixNoLockAsync(coins);
			}
		}

		public async Task DequeueAllCoinsFromMixAsync()
		{
			using (await MixLock.LockAsync())
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetAllQueuedCoins().ToArray());
			}
		}

		private async Task DequeueAllCoinsFromMixNoLockAsync()
		{
			await DequeueCoinsFromMixNoLockAsync(State.GetAllQueuedCoins().ToArray());
		}

		private async Task DequeueCoinsFromMixNoLockAsync(params (uint256 txid, uint index)[] coins)
		{
			List<Exception> exceptions = new List<Exception>();

			foreach (var coinReference in coins)
			{
				var coinToDequeue = State.GetSingleOrDefaultCoin(coinReference);
				if (coinToDequeue is null) continue;

				foreach (long roundId in State.GetPassivelyMixingRounds())
				{
					var round = State.GetSingleOrDefaultRound(roundId);
					if (round is null) throw new NotSupportedException("This is impossible.");

					if (round.CoinsRegistered.Contains(coinToDequeue))
					{
						try
						{
							await round.AliceClient.PostUnConfirmationAsync(); // AliceUniqueId must be there.
							State.ClearRoundRegistration(round.State.RoundId);
						}
						catch (Exception ex)
						{
							if (coinToDequeue.Unspent)
							{
								exceptions.Add(ex);
							}
						}
					}
				}

				foreach (long roundId in State.GetActivelyMixingRounds())
				{
					var round = State.GetSingleOrDefaultRound(roundId);
					if (round is null) continue;

					if (!coinToDequeue.Unspent) // If coin was spent, well that sucks, except if it was spent by the tumbler in signing phase.
					{
						State.ClearRoundRegistration(round.State.RoundId);
						continue;
					}
					if (round.CoinsRegistered.Contains(coinToDequeue))
					{
						exceptions.Add(new NotSupportedException($"Cannot deque coin in {round.State.Phase} phase. Coin: {coinToDequeue.Index}:{coinToDequeue.TransactionId}."));
					}
				}

				SmartCoin coinWaitingForMix = State.GetSingleOrDefaultFromWaitingList(coinToDequeue);
				if (!(coinWaitingForMix is null)) // If it is not being mixed, we can just remove it.
				{
					RemoveCoin(coinWaitingForMix);
				}
			}

			if (exceptions.Count == 1)
			{
				throw exceptions.Single();
			}

			if (exceptions.Count > 0)
			{
				throw new AggregateException(exceptions);
			}
		}

		private void RemoveCoin(SmartCoin coinWaitingForMix)
		{
			State.RemoveCoinFromWaitingList(coinWaitingForMix);
			coinWaitingForMix.CoinJoinInProgress = false;
			coinWaitingForMix.Secret = null;
			if (coinWaitingForMix.Label == "ZeroLink Change" && coinWaitingForMix.Unspent)
			{
				coinWaitingForMix.Label = "ZeroLink Dequeued Change";
				var key = KeyManager.GetKeys().SingleOrDefault(x => x.GetP2wpkhScript() == coinWaitingForMix.ScriptPubKey);
				if (!(key is null))
				{
					key.SetLabel(coinWaitingForMix.Label, KeyManager);
				}
			}
			CoinDequeued?.Invoke(this, coinWaitingForMix);
			Logger.LogInfo<CcjClient>($"Coin dequeued: {coinWaitingForMix.Index}:{coinWaitingForMix.TransactionId}.");
		}

		public async Task StopAsync()
		{
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			Cancel?.Cancel();
			while (IsStopping)
			{
				Task.Delay(50).GetAwaiter().GetResult(); // DO NOT MAKE IT ASYNC (.NET Core threading brainfart)
			}

			Cancel?.Dispose();

			using (await MixLock.LockAsync())
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());

				SatoshiClient?.Dispose();
				State.DisposeAllAliceClients();

				IEnumerable<(uint256 txid, uint index)> allCoins = State.GetAllQueuedCoins();
				foreach (var coinReference in allCoins)
				{
					try
					{
						var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
						if (coin is null)
						{
							continue; // The coin isn't present anymore. Good. This should never happen though.
						}
						await DequeueCoinsFromMixNoLockAsync((coin.TransactionId, coin.Index));
					}
					catch (Exception ex)
					{
						Logger.LogError<CcjClient>("Couldn't dequeue all coins. Some coins will likely be banned from mixing.");
						if (ex is AggregateException)
						{
							var aggrEx = ex as AggregateException;
							foreach (var innerEx in aggrEx.InnerExceptions)
							{
								Logger.LogError<CcjClient>(innerEx);
							}
						}
						else
						{
							Logger.LogError<CcjClient>(ex);
						}
					}
				}
			}
		}
	}
}
