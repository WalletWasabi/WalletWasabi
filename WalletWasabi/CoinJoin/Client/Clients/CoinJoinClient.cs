using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Services;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class CoinJoinClient
	{
		private const long StateNotStarted = 0;

		private const long StateRunning = 1;

		private const long StateStopping = 2;

		private const long StateStopped = 3;

		private long _frequentStatusProcessingIfNotMixing;

		/// <summary>
		/// Value can be any of: <see cref="StateNotStarted"/>, <see cref="StateRunning"/>, <see cref="StateStopping"/> and <see cref="StateStopped"/>.
		/// </summary>
		private long _running;

		private long _statusProcessing;

		public CoinJoinClient(
			WasabiSynchronizer synchronizer,
			Network network,
			KeyManager keyManager,
			Kitchen kitchen)
		{
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			DestinationKeyManager = KeyManager;
			Synchronizer = Guard.NotNull(nameof(synchronizer), synchronizer);
			CcjHostUriAction = Synchronizer.HttpClientFactory.BackendUriGetter;
			CoordinatorFeepercentToCheck = null;
			Kitchen = Guard.NotNull(nameof(kitchen), kitchen);

			ExposedLinks = new ConcurrentDictionary<OutPoint, IEnumerable<HdPubKeyBlindedPair>>();
			_running = StateNotStarted;
			Cancel = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			State = new ClientState();
			MixLock = new AsyncLock();
			_statusProcessing = 0;
			DelayedRoundRegistration = null;

			Synchronizer.ResponseArrived += Synchronizer_ResponseArrivedAsync;

			var lastResponse = Synchronizer.LastResponse;
			if (lastResponse is { })
			{
				AbandonedTasks.AddAndClearCompleted(TryProcessStatusAsync(Synchronizer.LastResponse.CcjRoundStates));
			}
		}

		public event EventHandler? StateUpdated;

		public event EventHandler<SmartCoin>? CoinQueued;

		public event EventHandler<DequeueResult>? OnDequeue;

		private AbandonedTasks AbandonedTasks { get; } = new AbandonedTasks();

		public Network Network { get; private set; }
		public KeyManager KeyManager { get; }
		public KeyManager DestinationKeyManager { get; set; }

		private ClientRoundRegistration DelayedRoundRegistration { get; set; }

		public Func<Uri> CcjHostUriAction { get; private set; }
		public WasabiSynchronizer Synchronizer { get; private set; }

		private decimal? CoordinatorFeepercentToCheck { get; set; }

		public ConcurrentDictionary<OutPoint, IEnumerable<HdPubKeyBlindedPair>> ExposedLinks { get; set; }

		private AsyncLock MixLock { get; set; }

		public ClientState State { get; }

		public bool IsRunning => Interlocked.Read(ref _running) == StateRunning;

		private CancellationTokenSource Cancel { get; set; }

		public bool IsDestinationSame => KeyManager.ExtPubKey == DestinationKeyManager.ExtPubKey;

		private Kitchen Kitchen { get; }

		private async void Synchronizer_ResponseArrivedAsync(object? sender, SynchronizeResponse e)
		{
			await TryProcessStatusAsync(e?.CcjRoundStates).ConfigureAwait(false);
		}

		public void Start()
		{
			if (Interlocked.CompareExchange(ref _running, StateRunning, StateNotStarted) != StateNotStarted)
			{
				return;
			}

			// The client is asking for status periodically, randomly between every 0.2 * connConfTimeout and 0.7 * connConfTimeout.
			// - if the GUI is at the mixer tab -Activate(), DeactivateIfNotMixing().
			// - if coins are queued to mix.
			// The client is asking for status periodically, randomly between every 2 to 7 seconds.
			// - if it is participating in a mix thats status >= connConf.

			// The client is triggered only when a status response arrives. The answer to the server is delayed randomly from 0 to 7 seconds.

			Task.Run(async () =>
			{
				try
				{
					Logger.LogInfo($"{nameof(CoinJoinClient)} is successfully initialized.");

					while (IsRunning)
					{
						try
						{
							using (await MixLock.LockAsync().ConfigureAwait(false))
							{
								await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

								// If stop was requested return.
								if (!IsRunning)
								{
									return;
								}

								// if mixing >= connConf
								if (State.GetActivelyMixingRounds().Any())
								{
									int delaySeconds = new Random().Next(2, 7);
									Synchronizer.MaxRequestIntervalForMixing = TimeSpan.FromSeconds(delaySeconds);
								}
								else if (Interlocked.Read(ref _frequentStatusProcessingIfNotMixing) == 1 || State.GetPassivelyMixingRounds().Any() || State.GetWaitingListCount() > 0)
								{
									double rand = double.Parse($"0.{new Random().Next(2, 6)}"); // randomly between every 0.2 * connConfTimeout - 7 and 0.6 * connConfTimeout
									int delaySeconds = Math.Max(0, (int)((rand * State.GetSmallestRegistrationTimeout()) - 7));

									Synchronizer.MaxRequestIntervalForMixing = TimeSpan.FromSeconds(delaySeconds);
								}
								else // dormant
								{
									Synchronizer.MaxRequestIntervalForMixing = TimeSpan.FromMinutes(3);
								}
							}
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);
						}
						finally
						{
							try
							{
								await Task.Delay(1000, Cancel.Token).ConfigureAwait(false);
							}
							catch (TaskCanceledException ex)
							{
								Logger.LogTrace(ex);
							}
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, StateStopped, StateStopping); // If IsStopping, make it stopped.
				}
			});
		}

		private async Task TryProcessStatusAsync(IEnumerable<RoundStateResponseBase> states)
		{
			states ??= Enumerable.Empty<RoundStateResponseBase>();

			if (Interlocked.Read(ref _statusProcessing) == 1) // It's ok to wait for status processing next time.
			{
				return;
			}

			try
			{
				Synchronizer.BlockRequests();

				Interlocked.Exchange(ref _statusProcessing, 1);
				using (await MixLock.LockAsync().ConfigureAwait(false))
				{
					// First, if there's delayed round registration update based on the state.
					if (DelayedRoundRegistration is { })
					{
						ClientRound roundRegistered = State.GetSingleOrDefaultRound(DelayedRoundRegistration.AliceClient.RoundId);
						roundRegistered.Registration = DelayedRoundRegistration;
						DelayedRoundRegistration = null; // Do not dispose.
					}

					await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

					State.UpdateRoundsByStates(ExposedLinks, states.ToArray());

					// If we do not have enough coin queued to register a round, then dequeue all.
					ClientRound registrableRound = State.GetRegistrableRoundOrDefault();
					if (registrableRound is { })
					{
						DequeueReason? reason = null;
						// If the coordinator increases fees, do not register. Let the users register manually again.
						if (CoordinatorFeepercentToCheck is { } && registrableRound.State.CoordinatorFeePercent > CoordinatorFeepercentToCheck)
						{
							reason = DequeueReason.CoordinatorFeeChanged;
						}
						else if (!registrableRound.State.HaveEnoughQueued(State.GetAllQueuedCoinAmounts()))
						{
							reason = DequeueReason.NotEnoughFundsEnqueued;
						}

						if (reason.HasValue)
						{
							await DequeueAllCoinsFromMixNoLockAsync(reason.Value).ConfigureAwait(false);
						}
					}
				}
				StateUpdated?.Invoke(this, null);

				int delaySeconds = new Random().Next(0, 7); // delay the response to defend timing attack privacy.

				if (Network == Network.RegTest)
				{
					delaySeconds = 0;
				}

				await Task.Delay(TimeSpan.FromSeconds(delaySeconds), Cancel.Token).ConfigureAwait(false);

				using (await MixLock.LockAsync().ConfigureAwait(false))
				{
					foreach (var ongoingRound in State.GetActivelyMixingRounds())
					{
						await TryProcessRoundStateAsync(ongoingRound).ConfigureAwait(false);
					}

					await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);
					ClientRound inputRegistrableRound = State.GetRegistrableRoundOrDefault();
					if (inputRegistrableRound is { })
					{
						if (inputRegistrableRound.Registration is null) // If did not register already, check what can we register.
						{
							await TryRegisterCoinsAsync(inputRegistrableRound).ConfigureAwait(false);
						}
						else // We registered, let's confirm we're online.
						{
							await TryConfirmConnectionAsync(inputRegistrableRound).ConfigureAwait(false);
						}
					}
				}
			}
			catch (TaskCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _statusProcessing, 0);
				Synchronizer.EnableRequests();
			}
		}

		private async Task TryProcessRoundStateAsync(ClientRound ongoingRound)
		{
			try
			{
				if (ongoingRound.State.Phase == RoundPhase.ConnectionConfirmation)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(RoundPhase.ConnectionConfirmation)) // If we did not already confirm connection in connection confirmation phase confirm it.
					{
						var (currentPhase, activeOutputs) = await ongoingRound.Registration.AliceClient.PostConfirmationAsync().ConfigureAwait(false);
						if (activeOutputs.Any())
						{
							ongoingRound.Registration.ActiveOutputs = activeOutputs;
						}
						ongoingRound.Registration.SetPhaseCompleted(RoundPhase.ConnectionConfirmation);
					}
				}
				else if (ongoingRound.State.Phase == RoundPhase.OutputRegistration)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(RoundPhase.OutputRegistration))
					{
						await RegisterOutputAsync(ongoingRound).ConfigureAwait(false);
					}
				}
				else if (ongoingRound.State.Phase == RoundPhase.Signing)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(RoundPhase.Signing))
					{
						Transaction unsignedCoinJoin = await ongoingRound.Registration.AliceClient.GetUnsignedCoinJoinAsync().ConfigureAwait(false);
						Dictionary<int, WitScript> myDic = SignCoinJoin(ongoingRound, unsignedCoinJoin);

						await ongoingRound.Registration.AliceClient.PostSignaturesAsync(myDic).ConfigureAwait(false);
						ongoingRound.Registration.SetPhaseCompleted(RoundPhase.Signing);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex); // Keep this in front of the logic (Logs will make more sense.)
				if (ex.Message.StartsWith("Not Found", StringComparison.Ordinal)) // Alice timed out.
				{
					State.ClearRoundRegistration(ongoingRound.RoundId);
				}
			}
		}

		private Dictionary<int, WitScript> SignCoinJoin(ClientRound ongoingRound, Transaction unsignedCoinJoin)
		{
			TxOut[] myOutputs = unsignedCoinJoin.Outputs
				.Where(x => x.ScriptPubKey == ongoingRound.Registration.ChangeAddress.ScriptPubKey
					|| ongoingRound.Registration.ActiveOutputs.Select(y => y.Address.ScriptPubKey).Contains(x.ScriptPubKey))
				.ToArray();
			Money amountBack = myOutputs.Sum(y => y.Value);

			// Make sure change is counted.
			Money minAmountBack = ongoingRound.CoinsRegistered.Sum(x => x.Amount); // Start with input sum.
																				   // Do outputs.lenght + 1 in case the server estimated the network fees wrongly due to insufficient data in an edge case.
			Money networkFeesAfterOutputs = ongoingRound.State.FeePerOutputs * (ongoingRound.Registration.AliceClient.RegisteredAddresses.Length + 1); // Use registered addresses here, because network fees are decided at inputregistration.
			Money networkFeesAfterInputs = ongoingRound.State.FeePerInputs * ongoingRound.Registration.CoinsRegistered.Count();
			Money networkFees = networkFeesAfterOutputs + networkFeesAfterInputs;
			minAmountBack -= networkFees; // Minus miner fee.

			IOrderedEnumerable<(Money value, int count)> indistinguishableOutputs = unsignedCoinJoin.GetIndistinguishableOutputs(includeSingle: false).OrderByDescending(x => x.count);
			foreach ((Money value, int count) in indistinguishableOutputs)
			{
				var mineCount = myOutputs.Count(x => x.Value == value);

				Money denomination = value;
				int anonset = Math.Min(110, count); // https://github.com/zkSNACKs/WalletWasabi/issues/1379
				Money expectedCoordinatorFee = denomination.Percentage(ongoingRound.State.CoordinatorFeePercent * anonset);
				for (int i = 0; i < mineCount; i++)
				{
					minAmountBack -= expectedCoordinatorFee; // Minus expected coordinator fee.
				}
			}

			// If there's no change output then coordinator protection may happened:
			bool gotChange = myOutputs.Select(x => x.ScriptPubKey).Contains(ongoingRound.Registration.ChangeAddress.ScriptPubKey);
			if (!gotChange)
			{
				Money minimumOutputAmount = Money.Coins(0.0001m); // If the change would be less than about $1 then add it to the coordinator.
				Money baseDenomination = indistinguishableOutputs.First().value;
				Money onePercentOfDenomination = baseDenomination.Percentage(1m); // If the change is less than about 1% of the newDenomination then add it to the coordinator fee.
				Money minimumChangeAmount = Math.Max(minimumOutputAmount, onePercentOfDenomination);

				minAmountBack -= minimumChangeAmount; // Minus coordinator protections (so it won't create bad coinjoins.)
			}

			if (amountBack < minAmountBack && !amountBack.Almost(minAmountBack, Money.Satoshis(1000))) // Just in case. Rounding error maybe?
			{
				Money diff = minAmountBack - amountBack;
				throw new NotSupportedException($"Coordinator did not add enough value to our outputs in the coinjoin. Missing: {diff.Satoshi} satoshis.");
			}

			var signedCoinJoin = unsignedCoinJoin.Clone();
			signedCoinJoin.Sign(
				ongoingRound
					.CoinsRegistered
					.Select(x =>
						(x.Secret ??= KeyManager.GetSecrets(Kitchen.SaltSoup(), x.ScriptPubKey).Single())
						.PrivateKey
						.GetBitcoinSecret(Network)),
				ongoingRound
					.Registration
					.CoinsRegistered
					.Select(x => x.Coin));

			var myDic = new Dictionary<int, WitScript>();

			for (int i = 0; i < signedCoinJoin.Inputs.Count; i++)
			{
				var input = signedCoinJoin.Inputs[i];
				if (ongoingRound.CoinsRegistered.Select(x => x.OutPoint).Contains(input.PrevOut))
				{
					myDic.Add(i, signedCoinJoin.Inputs[i].WitScript);
				}
			}

			return myDic;
		}

		private async Task RegisterOutputAsync(ClientRound ongoingRound)
		{
			IEnumerable<OutPoint> registeredInputs = ongoingRound.Registration.CoinsRegistered.Select(x => x.OutPoint);

			var shuffledOutputs = ongoingRound.Registration.ActiveOutputs.ToList();
			shuffledOutputs.Shuffle();
			foreach (var activeOutput in shuffledOutputs)
			{
				IHttpClient httpClient = Synchronizer.HttpClientFactory.NewBackendHttpClient(Mode.NewCircuitPerRequest);
				var bobClient = new BobClient(httpClient);
				if (!await bobClient.PostOutputAsync(ongoingRound.RoundId, activeOutput).ConfigureAwait(false))
				{
					Logger.LogWarning($"Round ({ongoingRound.State.RoundId}) Bobs did not have enough time to post outputs before timeout. If you see this message, contact nopara73, so he can optimize the phase timeout periods to the worst Internet/Tor connections, which may be yours.");
					break;
				}

				// Unblind our exposed links.
				foreach (OutPoint input in registeredInputs)
				{
					if (ExposedLinks.ContainsKey(input)) // Should never not contain, but oh well, let's not disrupt the round for this.
					{
						var found = ExposedLinks[input].FirstOrDefault(x => x.Key.GetP2wpkhAddress(Network) == activeOutput.Address);
						if (found is { })
						{
							found.IsBlinded = false;
						}
						else
						{
							// Should never happen, but oh well we can autocorrect it so why not.
							if (!DestinationKeyManager.TryGetKeyForScriptPubKey(activeOutput.Address.ScriptPubKey, out HdPubKey? hdPubKey)
								&& !KeyManager.TryGetKeyForScriptPubKey(activeOutput.Address.ScriptPubKey, out hdPubKey))
							{
								throw new NotSupportedException($"Couldn't get the key for the script. Address: {activeOutput.Address}.");
							}
							ExposedLinks[input] = ExposedLinks[input].Append(new HdPubKeyBlindedPair(hdPubKey, false));
						}
					}
				}
			}

			ongoingRound.Registration.SetPhaseCompleted(RoundPhase.OutputRegistration);
			Logger.LogInfo($"Round ({ongoingRound.State.RoundId}) Bob Posted outputs: {ongoingRound.Registration.ActiveOutputs.Count()}.");
		}

		private async Task TryConfirmConnectionAsync(ClientRound inputRegistrableRound)
		{
			try
			{
				var (currentPhase, activeOutputs) = await inputRegistrableRound.Registration.AliceClient.PostConfirmationAsync().ConfigureAwait(false);

				if (activeOutputs.Any())
				{
					inputRegistrableRound.Registration.ActiveOutputs = activeOutputs;
				}

				if (currentPhase > RoundPhase.InputRegistration) // Then the phase went to connection confirmation (probably).
				{
					inputRegistrableRound.Registration.SetPhaseCompleted(RoundPhase.ConnectionConfirmation);
					inputRegistrableRound.State.Phase = currentPhase;
				}
			}
			catch (Exception ex)
			{
				if (ex.Message.StartsWith("Not Found", StringComparison.Ordinal)) // Alice timed out.
				{
					State.ClearRoundRegistration(inputRegistrableRound.State.RoundId);
				}
				Logger.LogError(ex);
			}
		}

		private async Task TryRegisterCoinsAsync(ClientRound inputRegistrableRound)
		{
			try
			{
				// Select the most suitable coins to register.
				List<OutPoint> registrableCoins = State.GetRegistrableCoins(
					inputRegistrableRound.State.MaximumInputCountPerPeer,
					inputRegistrableRound.State.Denomination,
					inputRegistrableRound.State.FeePerInputs,
					inputRegistrableRound.State.FeePerOutputs).ToList();

				// If there are no suitable coins to register return.
				if (!registrableCoins.Any())
				{
					return;
				}

				var state = inputRegistrableRound.State;
				(HdPubKey change, IEnumerable<HdPubKey> actives) outputAddresses = GetOutputsToRegister(state.Denomination, state.MixLevelCount, registrableCoins);

				AliceClientBase? aliceClient = null;
				try
				{
					aliceClient = await CreateAliceClientAsync(inputRegistrableRound.RoundId, registrableCoins, outputAddresses).ConfigureAwait(false);
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("Input is banned", StringComparison.InvariantCultureIgnoreCase))
				{
					string[] parts = ex.Message.Split(new[] { "Input is banned from participation for ", " minutes: " }, StringSplitOptions.RemoveEmptyEntries);
					string minutesString = parts[1];
					int minuteInt = int.Parse(minutesString);
					string bannedInputString = parts[2].TrimEnd('.');
					string[] bannedInputStringParts = bannedInputString.Split(':', StringSplitOptions.RemoveEmptyEntries);
					OutPoint coinReference = new(new uint256(bannedInputStringParts[1]), uint.Parse(bannedInputStringParts[0]));
					SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This should never happen.");
					}

					coin.BannedUntilUtc = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(minuteInt);

					Logger.LogWarning(ex.Message.Split('\n')[1]);

					await DequeueCoinsFromMixNoLockAsync(coinReference, DequeueReason.Banned).ConfigureAwait(false);
					return;
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("Provided input is not unspent", StringComparison.InvariantCultureIgnoreCase))
				{
					string[] parts = ex.Message.Split(new[] { "Provided input is not unspent: " }, StringSplitOptions.RemoveEmptyEntries);
					string spentInputString = parts[1].TrimEnd('.');
					string[] bannedInputStringParts = spentInputString.Split(':', StringSplitOptions.RemoveEmptyEntries);
					OutPoint coinReference = new(new uint256(bannedInputStringParts[1]), uint.Parse(bannedInputStringParts[0]));
					SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This should never happen.");
					}

					coin.SpentAccordingToBackend = true;

					Logger.LogWarning(ex.Message.Split('\n')[1]);

					await DequeueCoinsFromMixNoLockAsync(coinReference, DequeueReason.Spent).ConfigureAwait(false);
					return;
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("No such running round in InputRegistration", StringComparison.InvariantCultureIgnoreCase))
				{
					Logger.LogInfo("Client tried to register a round that is not in InputRegistration anymore. Trying again later.");
					return;
				}
				catch (HttpRequestException ex) when (RpcErrorTools.IsTooLongMempoolChainError(ex.Message))
				{
					Logger.LogInfo("Coordinator failed because too much unconfirmed parent transactions. Trying again later.");
					return;
				}

				var coinsRegistered = new List<SmartCoin>();
				foreach (OutPoint coinReference in registrableCoins)
				{
					var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This should never happen.");
					}

					coinsRegistered.Add(coin);
					State.RemoveCoinFromWaitingList(coin);
				}

				var registration = new ClientRoundRegistration(aliceClient, coinsRegistered, outputAddresses.change.GetP2wpkhAddress(Network));

				ClientRound roundRegistered = State.GetSingleOrDefaultRound(aliceClient.RoundId);
				if (roundRegistered is null)
				{
					// If our SatoshiClient does not yet know about the round, because of delay, then delay the round registration.
					DelayedRoundRegistration = registration;
				}

				roundRegistered.Registration = registration;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		protected (HdPubKey change, IEnumerable<HdPubKey> active) GetOutputsToRegister(Money baseDenomination, int mixingLevelCount, IEnumerable<OutPoint> coinsToRegister)
		{
			// Figure out how many mixing level we need to register active outputs.
			Money inputSum = Money.Zero;
			foreach (OutPoint coinReference in coinsToRegister)
			{
				SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
				inputSum += coin.Amount;
			}

			int maximumMixingLevelCount = 1;
			var denominations = new List<Money>
			{
				baseDenomination
			};

			for (int i = 1; i < mixingLevelCount; i++)
			{
				Money denom = denominations.Last() * 2;
				denominations.Add(denom);
				if (inputSum > denom)
				{
					maximumMixingLevelCount = i + 1;
				}
			}

			CleanNonLockedExposedKeys();
			var keysToSurelyRegister = ExposedLinks.Where(x => coinsToRegister.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Key).ToArray();
			var keysTryNotToRegister = ExposedLinks.SelectMany(x => x.Value).Select(x => x.Key).Except(keysToSurelyRegister).ToArray();

			// Get all locked internal keys we have and assert we have enough.
			DestinationKeyManager.AssertLockedInternalKeysIndexed(howMany: maximumMixingLevelCount + 1);
			IEnumerable<HdPubKey> allLockedInternalKeys = DestinationKeyManager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked && !keysTryNotToRegister.Contains(x));

			// If any of our inputs have exposed address relationship then prefer that.
			allLockedInternalKeys = keysToSurelyRegister.Concat(allLockedInternalKeys).Distinct();

			// Prefer not to bloat the wallet:
			if (keysTryNotToRegister.Length >= DestinationKeyManager.MinGapLimit / 2)
			{
				allLockedInternalKeys = allLockedInternalKeys.Concat(keysTryNotToRegister).Distinct();
			}

			var newKeys = new List<HdPubKey>();
			for (int i = allLockedInternalKeys.Count(); i <= maximumMixingLevelCount + 1; i++)
			{
				HdPubKey k = DestinationKeyManager.GenerateNewKey(SmartLabel.Empty, KeyState.Locked, isInternal: true, toFile: false);
				newKeys.Add(k);
			}
			allLockedInternalKeys = allLockedInternalKeys.Concat(newKeys);

			// Select the change and active keys to register and label them accordingly.
			HdPubKey change = allLockedInternalKeys.First();

			var actives = new List<HdPubKey>();
			foreach (HdPubKey active in allLockedInternalKeys.Skip(1).Take(maximumMixingLevelCount))
			{
				actives.Add(active);
			}

			// Remember which links we are exposing.
			var outLinks = new List<HdPubKeyBlindedPair>
			{
				new HdPubKeyBlindedPair(change, isBlinded: false)
			};
			foreach (var active in actives)
			{
				outLinks.Add(new HdPubKeyBlindedPair(active, isBlinded: true));
			}
			foreach (OutPoint coin in coinsToRegister)
			{
				if (!ExposedLinks.TryAdd(coin, outLinks))
				{
					var newOutLinks = new List<HdPubKeyBlindedPair>();
					foreach (HdPubKeyBlindedPair link in ExposedLinks[coin])
					{
						newOutLinks.Add(link);
					}
					foreach (HdPubKeyBlindedPair link in outLinks)
					{
						var found = newOutLinks.FirstOrDefault(x => x == link);

						if (found is null)
						{
							newOutLinks.Add(link);
						}
						else // If already in it then update the blinded value if it's getting exposed just now. (eg. the change)
						{
							if (found.IsBlinded)
							{
								found.IsBlinded = link.IsBlinded;
							}
						}
					}

					ExposedLinks[coin] = newOutLinks;
				}
			}

			// Save our modifications in the keymanager before we give back the selected keys.
			DestinationKeyManager.ToFile();
			return (change, actives);
		}

		private void CleanNonLockedExposedKeys()
		{
			// Remove non-locked exposed keys.
			foreach (var key in ExposedLinks.Keys.ToArray())
			{
				if (ExposedLinks.TryGetValue(key, out var links))
				{
					var lockedKeys = links.Where(x => x.Key.KeyState == KeyState.Locked).ToArray();
					if (lockedKeys.Any())
					{
						ExposedLinks.AddOrReplace(key, lockedKeys);
					}
					else
					{
						ExposedLinks.TryRemove(key, out _);
					}
				}
			}
		}

		public async Task QueueCoinsToMixAsync(params SmartCoin[] coins)
			=> await QueueCoinsToMixAsync(coins as IEnumerable<SmartCoin>).ConfigureAwait(false);

		public async Task QueueCoinsToMixAsync(IEnumerable<SmartCoin> coins)
		{
			await QueueCoinsToMixAsync(Kitchen.SaltSoup(), coins).ConfigureAwait(false);
		}

		public void ActivateFrequentStatusProcessing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 1);
		}

		public void DeactivateFrequentStatusProcessingIfNotMixing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 0);
		}

		public async Task<IEnumerable<SmartCoin>> QueueCoinsToMixAsync(string password, params SmartCoin[] coins)
			=> await QueueCoinsToMixAsync(password, coins as IEnumerable<SmartCoin>).ConfigureAwait(false);

		public async Task<IEnumerable<SmartCoin>> QueueCoinsToMixAsync(string password, IEnumerable<SmartCoin> coins)
		{
			if (coins is null || !coins.Any())
			{
				return Enumerable.Empty<SmartCoin>();
			}

			var successful = new List<SmartCoin>();
			using (await MixLock.LockAsync().ConfigureAwait(false))
			{
				await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

				// Every time the user enqueues (intentionally writes in password) then the coordinator fee percent must be noted and dequeue later if changes.
				ClientRound latestRound = State.GetLatestRoundOrDefault();
				CoordinatorFeepercentToCheck = latestRound?.State?.CoordinatorFeePercent;

				var except = new List<SmartCoin>();

				foreach (SmartCoin coin in coins)
				{
					if (State.Contains(coin))
					{
						successful.Add(coin);
						except.Add(coin);
						continue;
					}

					if (!coin.IsAvailable())
					{
						except.Add(coin);
						continue;
					}
				}

				var coinsExcept = coins.Except(except);
				var secPubs = KeyManager.GetSecretsAndPubKeyPairs(password, coinsExcept.Select(x => x.ScriptPubKey).ToArray());

				Kitchen.Cook(password);

				foreach (SmartCoin coin in coinsExcept)
				{
					coin.Secret = secPubs.Single(x => x.pubKey.P2wpkhScript == coin.ScriptPubKey).secret;

					coin.CoinJoinInProgress = true;

					State.AddCoinToWaitingList(coin);
					successful.Add(coin);
					Logger.LogInfo($"Coin queued: {coin.Index}:{coin.TransactionId}.");
				}
			}

			foreach (var coin in successful)
			{
				CoinQueued?.Invoke(this, coin);
			}
			return successful;
		}

		public async Task DequeueCoinsFromMixAsync(SmartCoin coin, DequeueReason reason)
		{
			await DequeueCoinsFromMixAsync(new[] { coin }, reason).ConfigureAwait(false);
		}

		public async Task DequeueCoinsFromMixAsync(IEnumerable<SmartCoin> coins, DequeueReason reason)
		{
			if (coins is null || !coins.Any())
			{
				return;
			}

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			try
			{
				using (await MixLock.LockAsync(cts.Token).ConfigureAwait(false))
				{
					await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

					await DequeueCoinsFromMixNoLockAsync(coins.Select(x => x.OutPoint).ToArray(), reason).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException)
			{
				await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray(), reason).ConfigureAwait(false);

				await DequeueCoinsFromMixNoLockAsync(coins.Select(x => x.OutPoint).ToArray(), reason).ConfigureAwait(false);
			}
		}

		public async Task DequeueCoinsFromMixAsync(OutPoint[] coins, DequeueReason reason)
		{
			if (coins is null || !coins.Any())
			{
				return;
			}

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			try
			{
				using (await MixLock.LockAsync(cts.Token).ConfigureAwait(false))
				{
					await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

					await DequeueCoinsFromMixNoLockAsync(coins, reason).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException)
			{
				await DequeueSpentCoinsFromMixNoLockAsync().ConfigureAwait(false);

				await DequeueCoinsFromMixNoLockAsync(coins, reason).ConfigureAwait(false);
			}
		}

		public async Task DequeueAllCoinsFromMixAsync(DequeueReason reason)
		{
			if (reason == DequeueReason.ApplicationExit && Synchronizer.BackendStatus == BackendStatus.NotConnected)
			{
				return;
			}
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			try
			{
				using (await MixLock.LockAsync(cts.Token).ConfigureAwait(false))
				{
					await DequeueAllCoinsFromMixNoLockAsync(reason).ConfigureAwait(false);
				}
			}
			catch (TaskCanceledException)
			{
				await DequeueAllCoinsFromMixNoLockAsync(reason).ConfigureAwait(false);
			}
		}

		private async Task DequeueAllCoinsFromMixNoLockAsync(DequeueReason reason)
		{
			await DequeueCoinsFromMixNoLockAsync(State.GetAllQueuedCoins().ToArray(), reason).ConfigureAwait(false);
		}

		private async Task DequeueSpentCoinsFromMixNoLockAsync()
		{
			await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray(), DequeueReason.Spent).ConfigureAwait(false);
		}

		private async Task DequeueCoinsFromMixNoLockAsync(OutPoint coin, DequeueReason reason)
		{
			await DequeueCoinsFromMixNoLockAsync(new[] { coin }, reason).ConfigureAwait(false);
		}

		private async Task<DequeueResult> DequeueCoinsFromMixNoLockAsync(OutPoint[] coins, DequeueReason reason)
		{
			if (coins is null || !coins.Any())
			{
				return new DequeueResult(ImmutableDictionary<DequeueReason, IEnumerable<SmartCoin>>.Empty, ImmutableDictionary<DequeueReason, IEnumerable<SmartCoin>>.Empty);
			}

			var successful = new Dictionary<DequeueReason, List<SmartCoin>>();
			var unsuccessful = new Dictionary<DequeueReason, List<SmartCoin>>();
			List<Exception> exceptions = new();

			foreach (var coinReference in coins)
			{
				var coinToDequeue = State.GetSingleOrDefaultCoin(coinReference);
				if (coinToDequeue is null)
				{
					continue;
				}

				foreach (var round in State.GetAllMixingRounds().Where(x => x.CoinsRegistered.Contains(coinToDequeue)))
				{
					Exception? exception = null;
					if (round.State.Phase == RoundPhase.InputRegistration)
					{
						try
						{
							await round.Registration.AliceClient.PostUnConfirmationAsync().ConfigureAwait(false); // AliceUniqueId must be there.
							State.ClearRoundRegistration(round.State.RoundId);
						}
						catch (Exception ex)
						{
							if (!coinToDequeue.IsSpent())
							{
								exception = ex;
							}
						}
					}
					else
					{
						// If coin is unspent we cannot dequeue.
						if (!coinToDequeue.IsSpent())
						{
							exception = new NotSupportedException($"Cannot deque coin in {round.State.Phase} phase. Coin: {coinToDequeue.Index}:{coinToDequeue.TransactionId}.");
						}
						// If coin is spent, then we're going to DoS the round, there's nothing to do about it, except if it was spent by the tumbler in signing phase.
						else
						{
							State.ClearRoundRegistration(round.State.RoundId);
						}
					}

					if (exception is { })
					{
						exceptions.Add(exception);
						unsuccessful.AddToValueList(DequeueReason.Mixing, coinToDequeue);
					}
				}

				SmartCoin coinWaitingForMix = State.GetSingleOrDefaultFromWaitingList(coinToDequeue);
				if (coinWaitingForMix is { }) // If it is not being mixed, we can just remove it.
				{
					State.RemoveCoinFromWaitingList(coinWaitingForMix);
					coinWaitingForMix.CoinJoinInProgress = false;
					coinWaitingForMix.Secret = null;
					successful.AddToValueList(reason, coinToDequeue);
					Logger.LogInfo($"Coin dequeued: {coinWaitingForMix.Index}:{coinWaitingForMix.TransactionId}. Reason: {reason}.");
				}
			}

			var result = new DequeueResult(successful.ToDictionary(x => x.Key, x => x.Value as IEnumerable<SmartCoin>), unsuccessful.ToDictionary(x => x.Key, x => x.Value as IEnumerable<SmartCoin>));

			if (result.Successful.Concat(result.Unsuccessful).Any())
			{
				OnDequeue?.Invoke(this, result);
			}

			if (exceptions.Count == 1)
			{
				throw exceptions.Single();
			}

			if (exceptions.Count > 0)
			{
				throw new AggregateException(exceptions);
			}
			return result;
		}

		public async Task StopAsync(CancellationToken cancel)
		{
			await DequeueAllCoinsFromMixGracefullyAsync(DequeueReason.ApplicationExit, cancel).ConfigureAwait(false);

			Synchronizer.ResponseArrived -= Synchronizer_ResponseArrivedAsync;

			Interlocked.CompareExchange(ref _running, StateStopping, StateRunning); // If running, make it stopping.
			Cancel.Cancel();

			while (Interlocked.CompareExchange(ref _running, StateStopped, StateNotStarted) == StateStopping)
			{
				await Task.Delay(50, cancel).ConfigureAwait(false);
			}

			Cancel.Dispose();

			using (await MixLock.LockAsync(cancel).ConfigureAwait(false))
			{
				IEnumerable<OutPoint> allCoins = State.GetAllQueuedCoins();
				foreach (var coinReference in allCoins)
				{
					try
					{
						var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
						if (coin is null)
						{
							continue; // The coin is not present anymore. Good. This should never happen though.
						}
						await DequeueCoinsFromMixNoLockAsync(coin.OutPoint, DequeueReason.ApplicationExit).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						Logger.LogError("Could not dequeue all coins. Some coins will likely be banned from mixing.");
						if (ex is AggregateException aggrEx)
						{
							foreach (var innerEx in aggrEx.InnerExceptions)
							{
								Logger.LogError(innerEx);
							}
						}
						else
						{
							Logger.LogError(ex);
						}
					}
				}
			}
			await AbandonedTasks.WhenAllAsync().ConfigureAwait(false);
		}

		public async Task DequeueAllCoinsFromMixGracefullyAsync(DequeueReason reason, CancellationToken cancel)
		{
			while (true)
			{
				cancel.ThrowIfCancellationRequested();

				try
				{
					await DequeueAllCoinsFromMixAsync(reason).ConfigureAwait(false);
					break;
				}
				catch
				{
					await Task.Delay(1000, cancel).ConfigureAwait(false); // wait, maybe the situation will change
				}
			}
		}

		private async Task<AliceClientBase> CreateAliceClientAsync(long roundId, List<OutPoint> registrableCoins, (HdPubKey change, IEnumerable<HdPubKey> actives) outputAddresses)
		{
			HttpClientFactory factory = Synchronizer.HttpClientFactory;

			IHttpClient satoshiHttpClient = factory.NewBackendHttpClient(Mode.NewCircuitPerRequest);
			SatoshiClient satoshiClient = new(satoshiHttpClient);
			RoundStateResponse4 state = (RoundStateResponse4)await satoshiClient.GetRoundStateAsync(roundId).ConfigureAwait(false);

			PubKey[] signerPubKeys = state.SignerPubKeys.ToArray();
			PublicNonceWithIndex[] numerateNonces = state.RPubKeys.ToArray();
			List<Requester> requesters = new();
			var blindedOutputScriptHashes = new List<BlindedOutputWithNonceIndex>();

			var registeredAddresses = new List<BitcoinAddress>();
			for (int i = 0; i < state.MixLevelCount; i++)
			{
				if (outputAddresses.actives.Count() <= i)
				{
					break;
				}

				BitcoinAddress address = outputAddresses.actives.Select(x => x.GetP2wpkhAddress(Network)).ElementAt(i);

				PubKey signerPubKey = signerPubKeys[i];
				var outputScriptHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(address.ScriptPubKey.ToBytes()));
				var requester = new Requester();
				(int n, PubKey r) = (numerateNonces[i].N, numerateNonces[i].R);
				var blindedMessage = requester.BlindMessage(outputScriptHash, r, signerPubKey);
				var blindedOutputScript = new BlindedOutputWithNonceIndex(n, blindedMessage);
				requesters.Add(requester);
				blindedOutputScriptHashes.Add(blindedOutputScript);
				registeredAddresses.Add(address);
			}

			byte[] blindedOutputScriptHashesByte = ByteHelpers.Combine(blindedOutputScriptHashes.Select(x => x.BlindedOutput.ToBytes()));
			uint256 blindedOutputScriptsHash = new(NBitcoin.Crypto.Hashes.SHA256(blindedOutputScriptHashesByte));

			var inputProofs = new List<InputProofModel>();
			foreach (OutPoint coinReference in registrableCoins)
			{
				SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
				if (coin is null)
				{
					throw new NotSupportedException("This should never happen.");
				}

				coin.Secret ??= KeyManager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var inputProof = new InputProofModel
				{
					Input = coin.OutPoint,
					Proof = coin.Secret.PrivateKey.SignCompact(blindedOutputScriptsHash)
				};
				inputProofs.Add(inputProof);
			}

			IHttpClient httpClient = Synchronizer.HttpClientFactory.NewHttpClient(CcjHostUriAction, Mode.NewCircuitPerRequest);
			return await AliceClientBase.CreateNewAsync(roundId, registeredAddresses, signerPubKeys, requesters, Network, outputAddresses.change.GetP2wpkhAddress(Network), blindedOutputScriptHashes, inputProofs, httpClient).ConfigureAwait(false);
		}
	}
}
