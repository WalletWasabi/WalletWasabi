using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.Services
{
	public class CcjClient
	{
		public Network Network { get; private set; }
		public KeyManager KeyManager { get; private set; }
		public bool IsQuitPending { get; set; }

		private ClientRoundRegistration DelayedRoundRegistration { get; set; }

		public Func<Uri> CcjHostUriAction { get; private set; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		private IPEndPoint TorSocks5EndPoint { get; set; }

		private decimal? CoordinatorFeepercentToCheck { get; set; }

		public ConcurrentDictionary<TxoRef, IEnumerable<HdPubKeyBlindedPair>> ExposedLinks { get; set; }

		private AsyncLock MixLock { get; set; }

		public CcjClientState State { get; private set; }

		public event EventHandler StateUpdated;

		public event EventHandler<SmartCoin> CoinQueued;

		public event EventHandler<SmartCoin> CoinDequeued;

		private long _frequentStatusProcessingIfNotMixing;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private long _statusProcessing;

		private CancellationTokenSource Cancel { get; set; }

		public CcjClient(
			WasabiSynchronizer synchronizer,
			Network network,
			KeyManager keyManager,
			Func<Uri> ccjHostUriAction,
			IPEndPoint torSocks5EndPoint)
		{
			Create(synchronizer, network, keyManager, ccjHostUriAction, torSocks5EndPoint);
		}

		public CcjClient(
			WasabiSynchronizer synchronizer,
			Network network,
			KeyManager keyManager,
			Uri ccjHostUri,
			IPEndPoint torSocks5EndPoint)
		{
			Create(synchronizer, network, keyManager, () => ccjHostUri, torSocks5EndPoint);
		}

		private void Create(WasabiSynchronizer synchronizer, Network network, KeyManager keyManager, Func<Uri> ccjHostUriAction, IPEndPoint torSocks5EndPoint)
		{
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			CcjHostUriAction = Guard.NotNull(nameof(ccjHostUriAction), ccjHostUriAction);
			Synchronizer = Guard.NotNull(nameof(synchronizer), synchronizer);
			TorSocks5EndPoint = torSocks5EndPoint;
			CoordinatorFeepercentToCheck = null;

			ExposedLinks = new ConcurrentDictionary<TxoRef, IEnumerable<HdPubKeyBlindedPair>>();
			_running = 0;
			Cancel = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			State = new CcjClientState();
			MixLock = new AsyncLock();
			_statusProcessing = 0;
			DelayedRoundRegistration = null;

			Synchronizer.ResponseArrived += Synchronizer_ResponseArrivedAsync;

			var lastResponse = Synchronizer.LastResponse;
			if (lastResponse != null)
			{
				_ = TryProcessStatusAsync(Synchronizer.LastResponse.CcjRoundStates);
			}
		}

		private async void Synchronizer_ResponseArrivedAsync(object sender, SynchronizeResponse e)
		{
			await TryProcessStatusAsync(e?.CcjRoundStates);
		}

		public void Start()
		{
			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
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
					Logger.LogInfo<CcjClient>("CcjClient is successfully initialized.");

					while (IsRunning)
					{
						try
						{
							using (await MixLock.LockAsync())
							{
								await DequeueSpentCoinsFromMixNoLockAsync();

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
									int delaySeconds = Math.Max(0, (int)(rand * State.GetSmallestRegistrationTimeout() - 7));

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
							Logger.LogError<CcjClient>(ex);
						}
						finally
						{
							try
							{
								await Task.Delay(1000, Cancel.Token);
							}
							catch (TaskCanceledException ex)
							{
								Logger.LogTrace<CcjClient>(ex);
							}
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		private async Task TryProcessStatusAsync(IEnumerable<CcjRunningRoundState> states)
		{
			states = states ?? Enumerable.Empty<CcjRunningRoundState>();

			if (Interlocked.Read(ref _statusProcessing) == 1) // It's ok to wait for status processing next time.
			{
				return;
			}

			try
			{
				Synchronizer.BlockRequests();

				Interlocked.Exchange(ref _statusProcessing, 1);
				using (await MixLock.LockAsync())
				{
					// First, if there's delayed round registration update based on the state.
					if (DelayedRoundRegistration != null)
					{
						CcjClientRound roundRegistered = State.GetSingleOrDefaultRound(DelayedRoundRegistration.AliceClient.RoundId);
						roundRegistered.Registration = DelayedRoundRegistration;
						DelayedRoundRegistration = null; // Don't dispose.
					}

					await DequeueSpentCoinsFromMixNoLockAsync();

					State.UpdateRoundsByStates(ExposedLinks, states.ToArray());

					// If we don't have enough coin queued to register a round, then dequeue all.
					CcjClientRound registrableRound = State.GetRegistrableRoundOrDefault();
					if (registrableRound != default)
					{
						// If the coordinator increases fees, don't register. Let the users register manually again.
						bool dequeueBecauseCoordinatorFeeChanged = false;
						if (CoordinatorFeepercentToCheck != default)
						{
							dequeueBecauseCoordinatorFeeChanged = registrableRound.State.CoordinatorFeePercent > CoordinatorFeepercentToCheck;
						}

						if (!registrableRound.State.HaveEnoughQueued(State.GetAllQueuedCoinAmounts().ToArray())
							|| dequeueBecauseCoordinatorFeeChanged)
						{
							await DequeueAllCoinsFromMixNoLockAsync("The total value of the registered coins is not enough or the coordinator's fee changed.");
						}
					}
				}
				StateUpdated?.Invoke(this, null);

				int delaySeconds = new Random().Next(0, 7); // delay the response to defend timing attack privacy.

				if (Network == Network.RegTest)
				{
					delaySeconds = 0;
				}

				await Task.Delay(TimeSpan.FromSeconds(delaySeconds), Cancel.Token);

				using (await MixLock.LockAsync())
				{
					foreach (long ongoingRoundId in State.GetActivelyMixingRounds())
					{
						await TryProcessRoundStateAsync(ongoingRoundId);
					}

					await DequeueSpentCoinsFromMixNoLockAsync();
					CcjClientRound inputRegistrableRound = State.GetRegistrableRoundOrDefault();
					if (inputRegistrableRound != null)
					{
						if (inputRegistrableRound.Registration is null) // If did not register already, check what can we register.
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
			finally
			{
				Interlocked.Exchange(ref _statusProcessing, 0);
				Synchronizer.EnableRequests();
			}
		}

		private async Task TryProcessRoundStateAsync(long ongoingRoundId)
		{
			try
			{
				var ongoingRound = State.GetSingleOrDefaultRound(ongoingRoundId);
				if (ongoingRound is null)
				{
					throw new NotSupportedException("This is impossible.");
				}

				if (ongoingRound.State.Phase == CcjRoundPhase.ConnectionConfirmation)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(CcjRoundPhase.ConnectionConfirmation)) // If we did not already confirmed connection in connection confirmation phase confirm it.
					{
						var res = await ongoingRound.Registration.AliceClient.PostConfirmationAsync();
						if (res.activeOutputs.Any())
						{
							ongoingRound.Registration.ActiveOutputs = res.activeOutputs;
						}
						ongoingRound.Registration.SetPhaseCompleted(CcjRoundPhase.ConnectionConfirmation);
					}
				}
				else if (ongoingRound.State.Phase == CcjRoundPhase.OutputRegistration)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(CcjRoundPhase.OutputRegistration))
					{
						await RegisterOutputAsync(ongoingRound);
					}
				}
				else if (ongoingRound.State.Phase == CcjRoundPhase.Signing)
				{
					if (!ongoingRound.Registration.IsPhaseActionsComleted(CcjRoundPhase.Signing))
					{
						Transaction unsignedCoinJoin = await ongoingRound.Registration.AliceClient.GetUnsignedCoinJoinAsync();
						Dictionary<int, WitScript> myDic = SignCoinJoin(ongoingRound, unsignedCoinJoin);

						await ongoingRound.Registration.AliceClient.PostSignaturesAsync(myDic);
						ongoingRound.Registration.SetPhaseCompleted(CcjRoundPhase.Signing);
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
			foreach ((Money value, int count) denomPair in indistinguishableOutputs)
			{
				var mineCount = myOutputs.Count(x => x.Value == denomPair.value);

				Money denomination = denomPair.value;
				int anonset = Math.Min(110, denomPair.count); // https://github.com/zkSNACKs/WalletWasabi/issues/1379
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
			signedCoinJoin.Sign(ongoingRound.CoinsRegistered.Select(x => x.Secret = x.Secret ?? KeyManager.GetSecrets(SaltSoup(), x.ScriptPubKey).Single()).ToArray(), ongoingRound.Registration.CoinsRegistered.Select(x => x.GetCoin()).ToArray());

			// Old way of signing, which randomly fails! https://github.com/zkSNACKs/WalletWasabi/issues/716#issuecomment-435498906
			// Must be fixed in NBitcoin.
			//var builder = Network.CreateTransactionBuilder();
			//var signedCoinJoin = builder
			//	.ContinueToBuild(unsignedCoinJoin)
			//	.AddKeys(ongoingRound.Registration.CoinsRegistered.Select(x => x.Secret = x.Secret ?? KeyManager.GetSecrets(OnePiece, x.ScriptPubKey).Single()).ToArray())
			//	.AddCoins(ongoingRound.Registration.CoinsRegistered.Select(x => x.GetCoin()))
			//	.BuildTransaction(true);

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
			IEnumerable<TxoRef> registeredInputs = ongoingRound.Registration.CoinsRegistered.Select(x => x.GetTxoRef());

			var shuffledOutputs = ongoingRound.Registration.ActiveOutputs.ToList();
			shuffledOutputs.Shuffle();
			foreach (var activeOutput in shuffledOutputs)
			{
				using (var bobClient = new BobClient(CcjHostUriAction, TorSocks5EndPoint))
				{
					if (!await bobClient.PostOutputAsync(ongoingRound.RoundId, activeOutput))
					{
						Logger.LogWarning<AliceClient>($"Round ({ongoingRound.State.RoundId}) Bobs did not have enough time to post outputs before timeout. If you see this message, contact nopara73, so he can optimize the phase timeout periods to the worst Internet/Tor connections, which may be yours.)");
						break;
					}

					// Unblind our exposed links.
					foreach (TxoRef input in registeredInputs)
					{
						if (ExposedLinks.ContainsKey(input)) // Should never not contain, but oh well, let's not disrupt the round for this.
						{
							var found = ExposedLinks[input].FirstOrDefault(x => x.Key.GetP2wpkhAddress(Network) == activeOutput.Address);
							if (found != default)
							{
								found.IsBlinded = false;
							}
							else
							{
								// Should never happen, but oh well we can autocorrect it so why not.
								ExposedLinks[input] = ExposedLinks[input].Append(new HdPubKeyBlindedPair(KeyManager.GetKeyForScriptPubKey(activeOutput.Address.ScriptPubKey), false));
							}
						}
					}
				}
			}

			ongoingRound.Registration.SetPhaseCompleted(CcjRoundPhase.OutputRegistration);
			Logger.LogInfo<AliceClient>($"Round ({ongoingRound.State.RoundId}) Bob Posted outputs: {ongoingRound.Registration.ActiveOutputs.Count()}.");
		}

		private async Task TryConfirmConnectionAsync(CcjClientRound inputRegistrableRound)
		{
			try
			{
				var res = await inputRegistrableRound.Registration.AliceClient.PostConfirmationAsync();

				if (res.activeOutputs.Any())
				{
					inputRegistrableRound.Registration.ActiveOutputs = res.activeOutputs;
				}

				if (res.currentPhase > CcjRoundPhase.InputRegistration) // Then the phase went to connection confirmation (probably).
				{
					inputRegistrableRound.Registration.SetPhaseCompleted(CcjRoundPhase.ConnectionConfirmation);
					inputRegistrableRound.State.Phase = res.currentPhase;
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
				// Select the most suitable coins to regiter.
				List<TxoRef> registrableCoins = State.GetRegistrableCoins(
					inputRegistrableRound.State.MaximumInputCountPerPeer,
					inputRegistrableRound.State.Denomination,
					inputRegistrableRound.State.FeePerInputs,
					inputRegistrableRound.State.FeePerOutputs).ToList();

				// If there are no suitable coins to register return.
				if (!registrableCoins.Any())
				{
					return;
				}

				(HdPubKey change, IEnumerable<HdPubKey> actives) outputAddresses = GetOutputsToRegister(inputRegistrableRound.State.Denomination, inputRegistrableRound.State.SchnorrPubKeys.Count(), registrableCoins);

				SchnorrPubKey[] schnorrPubKeys = inputRegistrableRound.State.SchnorrPubKeys.ToArray();
				List<Requester> requesters = new List<Requester>();
				var blindedOutputScriptHashes = new List<uint256>();

				var registeredAddresses = new List<BitcoinAddress>();
				for (int i = 0; i < schnorrPubKeys.Length; i++)
				{
					if (outputAddresses.actives.Count() <= i)
					{
						break;
					}

					BitcoinAddress address = outputAddresses.actives.Select(x => x.GetP2wpkhAddress(Network)).ElementAt(i);

					SchnorrPubKey schnorrPubKey = schnorrPubKeys[i];
					var outputScriptHash = new uint256(Hashes.SHA256(address.ScriptPubKey.ToBytes()));
					var requester = new Requester();
					uint256 blindedOutputScriptHash = requester.BlindMessage(outputScriptHash, schnorrPubKey);
					requesters.Add(requester);
					blindedOutputScriptHashes.Add(blindedOutputScriptHash);
					registeredAddresses.Add(address);
				}

				byte[] blindedOutputScriptHashesByte = ByteHelpers.Combine(blindedOutputScriptHashes.Select(x => x.ToBytes()));
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedOutputScriptHashesByte));

				var inputProofs = new List<InputProofModel>();
				foreach (TxoRef coinReference in registrableCoins)
				{
					SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This is impossible.");
					}

					coin.Secret = coin.Secret ?? KeyManager.GetSecrets(SaltSoup(), coin.ScriptPubKey).Single();
					var inputProof = new InputProofModel {
						Input = coin.GetTxoRef(),
						Proof = coin.Secret.PrivateKey.SignCompact(blindedOutputScriptsHash)
					};
					inputProofs.Add(inputProof);
				}

				AliceClient aliceClient = null;
				try
				{
					aliceClient = await AliceClient.CreateNewAsync(inputRegistrableRound.RoundId, registeredAddresses, schnorrPubKeys, requesters, Network, outputAddresses.change.GetP2wpkhAddress(Network), blindedOutputScriptHashes, inputProofs, CcjHostUriAction, TorSocks5EndPoint);
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("Input is banned", StringComparison.InvariantCultureIgnoreCase))
				{
					string[] parts = ex.Message.Split(new[] { "Input is banned from participation for ", " minutes: " }, StringSplitOptions.RemoveEmptyEntries);
					string minutesString = parts[1];
					int minuteInt = int.Parse(minutesString);
					string bannedInputString = parts[2].TrimEnd('.');
					string[] bannedInputStringParts = bannedInputString.Split(':', StringSplitOptions.RemoveEmptyEntries);
					TxoRef coinReference = new TxoRef(new uint256(bannedInputStringParts[1]), uint.Parse(bannedInputStringParts[0]));
					SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This is impossible.");
					}

					coin.BannedUntilUtc = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(minuteInt);

					Logger.LogWarning<CcjClient>(ex.Message.Split('\n')[1]);

					await DequeueCoinsFromMixNoLockAsync(coinReference, "Failed to register the coin with the coordinator.");
					aliceClient?.Dispose();
					return;
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("Provided input is not unspent", StringComparison.InvariantCultureIgnoreCase))
				{
					string[] parts = ex.Message.Split(new[] { "Provided input is not unspent: " }, StringSplitOptions.RemoveEmptyEntries);
					string spentInputString = parts[1].TrimEnd('.');
					string[] bannedInputStringParts = spentInputString.Split(':', StringSplitOptions.RemoveEmptyEntries);
					TxoRef coinReference = new TxoRef(new uint256(bannedInputStringParts[1]), uint.Parse(bannedInputStringParts[0]));
					SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This is impossible.");
					}

					coin.SpentAccordingToBackend = true;

					Logger.LogWarning<CcjClient>(ex.Message.Split('\n')[1]);

					await DequeueCoinsFromMixNoLockAsync(coinReference, "Failed to register the coin with the coordinator. The coin is already spent.");
					aliceClient?.Dispose();
					return;
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("No such running round in InputRegistration.", StringComparison.InvariantCultureIgnoreCase))
				{
					Logger.LogInfo<CcjClient>("Client tried to register a round that is not in InputRegistration anymore. Trying again later.");
					aliceClient?.Dispose();
					return;
				}
				catch (HttpRequestException ex) when (ex.Message.Contains("too-long-mempool-chain", StringComparison.InvariantCultureIgnoreCase))
				{
					Logger.LogInfo<CcjClient>("Coordinator failed because too much unconfirmed parent transactions. Trying again later.");
					aliceClient?.Dispose();
					return;
				}

				var coinsRegistered = new List<SmartCoin>();
				foreach (TxoRef coinReference in registrableCoins)
				{
					var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
					if (coin is null)
					{
						throw new NotSupportedException("This is impossible.");
					}

					coinsRegistered.Add(coin);
					State.RemoveCoinFromWaitingList(coin);
				}

				var registration = new ClientRoundRegistration(aliceClient, coinsRegistered, outputAddresses.change.GetP2wpkhAddress(Network));

				CcjClientRound roundRegistered = State.GetSingleOrDefaultRound(aliceClient.RoundId);
				if (roundRegistered is null)
				{
					// If our SatoshiClient doesn't yet know about the round, because of delay, then delay the round registration.
					DelayedRoundRegistration?.Dispose();
					DelayedRoundRegistration = registration;
				}

				roundRegistered.Registration = registration;
			}
			catch (Exception ex)
			{
				Logger.LogError<CcjClient>(ex);
			}
		}

		private (HdPubKey change, IEnumerable<HdPubKey> active) GetOutputsToRegister(Money baseDenomination, int mixingLevelCount, IEnumerable<TxoRef> coinsToRegister)
		{
			// Figure out how many mixing level we need to register active outputs.
			Money inputSum = Money.Zero;
			foreach (TxoRef coinReference in coinsToRegister)
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

			string changeLabel = "ZeroLink Change";
			string activeLabel = "ZeroLink Mixed Coin";

			var keysToSurelyRegister = ExposedLinks.Where(x => coinsToRegister.Contains(x.Key)).SelectMany(x => x.Value).Select(x => x.Key).ToArray();
			var keysTryNotToRegister = ExposedLinks.SelectMany(x => x.Value).Select(x => x.Key).Except(keysToSurelyRegister).ToArray();

			// Get all locked internal keys we have and assert we have enough.
			KeyManager.AssertLockedInternalKeysIndexed(howMany: maximumMixingLevelCount + 1);
			IEnumerable<HdPubKey> allLockedInternalKeys = KeyManager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked && !keysTryNotToRegister.Contains(x));

			// If any of our inputs have exposed address relationship then prefer that.
			allLockedInternalKeys = keysToSurelyRegister.Concat(allLockedInternalKeys).Distinct();

			// Prefer not to bloat the wallet:
			if (allLockedInternalKeys.Count() <= maximumMixingLevelCount)
			{
				allLockedInternalKeys = allLockedInternalKeys.Concat(keysTryNotToRegister).Distinct();
			}

			var newKeys = new List<HdPubKey>();
			for (int i = allLockedInternalKeys.Count(); i <= maximumMixingLevelCount + 1; i++)
			{
				HdPubKey k = KeyManager.GenerateNewKey("", KeyState.Locked, isInternal: true, toFile: false);
				newKeys.Add(k);
			}
			allLockedInternalKeys = allLockedInternalKeys.Concat(newKeys);

			// Select the change and active keys to register and label them accordingly.
			HdPubKey change = allLockedInternalKeys.First();
			change.SetLabel(changeLabel);

			var actives = new List<HdPubKey>();
			foreach (HdPubKey active in allLockedInternalKeys.Skip(1).Take(maximumMixingLevelCount))
			{
				actives.Add(active);
				active.SetLabel(activeLabel);
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
			foreach (TxoRef coin in coinsToRegister)
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
						HdPubKeyBlindedPair found = newOutLinks.FirstOrDefault(x => x == link);

						if (found == default)
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
			KeyManager.ToFile();
			return (change, actives);
		}

		public async Task QueueCoinsToMixAsync(params SmartCoin[] coins)
		{
			await QueueCoinsToMixAsync(SaltSoup(), coins);
		}

		public void ActivateFrequentStatusProcessing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 1);
		}

		public void DeactivateFrequentStatusProcessingIfNotMixing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 0);
		}

		private string Salt { get; set; } = null;
		private string Soup { get; set; } = null;
		private object RefrigeratorLock { get; } = new object();

		public async Task<IEnumerable<SmartCoin>> QueueCoinsToMixAsync(string password, params SmartCoin[] coins)
		{
			if (coins is null || !coins.Any() || IsQuitPending)
			{
				return Enumerable.Empty<SmartCoin>();
			}

			var successful = new List<SmartCoin>();
			using (await MixLock.LockAsync())
			{
				await DequeueSpentCoinsFromMixNoLockAsync();

				// Every time the user enqueues (intentionally writes in password) then the coordinator fee percent must be noted and dequeue later if changes.
				CcjClientRound latestRound = State.GetLatestRoundOrDefault();
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

					if (coin.Unavailable)
					{
						except.Add(coin);
						continue;
					}
				}

				var coinsExcept = coins.Except(except);
				var secPubs = KeyManager.GetSecretsAndPubKeyPairs(password, coinsExcept.Select(x => x.ScriptPubKey).ToArray());

				Cook(password);

				foreach (SmartCoin coin in coinsExcept)
				{
					coin.Secret = secPubs.Single(x => x.pubKey.P2wpkhScript == coin.ScriptPubKey).secret;

					coin.CoinJoinInProgress = true;

					State.AddCoinToWaitingList(coin);
					successful.Add(coin);
					Logger.LogInfo<CcjClient>($"Coin queued: {coin.Index}:{coin.TransactionId}.");
				}
			}

			foreach (var coin in successful)
			{
				CoinQueued?.Invoke(this, coin);
			}
			return successful;
		}

		public async Task DequeueCoinsFromMixAsync(SmartCoin coin, string reason)
		{
			await DequeueCoinsFromMixAsync(new[] { coin }, reason);
		}

		public async Task DequeueCoinsFromMixAsync(IEnumerable<SmartCoin> coins, string reason)
		{
			if (coins is null || !coins.Any())
			{
				return;
			}

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using (await MixLock.LockAsync(cts.Token))
					{
						await DequeueSpentCoinsFromMixNoLockAsync();

						await DequeueCoinsFromMixNoLockAsync(coins.Select(x => x.GetTxoRef()).ToArray(), reason);
					}
				}
				catch (TaskCanceledException)
				{
					await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray(), reason);

					await DequeueCoinsFromMixNoLockAsync(coins.Select(x => x.GetTxoRef()).ToArray(), reason);
				}
			}
		}

		public bool HasIngredients => Salt != null && Soup != null;

		private string SaltSoup()
		{
			if (!HasIngredients)
			{
				return null;
			}

			string res;
			lock (RefrigeratorLock)
			{
				res = StringCipher.Decrypt(Soup, Salt);
			}

			Cook(res);

			return res;
		}

		private void Cook(string ingredients)
		{
			lock (RefrigeratorLock)
			{
				ingredients = ingredients ?? "";

				Salt = TokenGenerator.GetUniqueKey(21);
				Soup = StringCipher.Encrypt(ingredients, Salt);
			}
		}

		public async Task DequeueCoinsFromMixAsync(TxoRef coin, string reason)
		{
			await DequeueCoinsFromMixAsync(new[] { coin }, reason);
		}

		public async Task DequeueCoinsFromMixAsync(TxoRef[] coins, string reason)
		{
			if (coins is null || !coins.Any())
			{
				return;
			}

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using (await MixLock.LockAsync(cts.Token))
					{
						await DequeueSpentCoinsFromMixNoLockAsync();

						await DequeueCoinsFromMixNoLockAsync(coins, reason);
					}
				}
				catch (TaskCanceledException)
				{
					await DequeueSpentCoinsFromMixNoLockAsync();

					await DequeueCoinsFromMixNoLockAsync(coins, reason);
				}
			}
		}

		public async Task DequeueAllCoinsFromMixAsync(string reason)
		{
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
			{
				try
				{
					using (await MixLock.LockAsync(cts.Token))
					{
						await DequeueAllCoinsFromMixNoLockAsync(reason);
					}
				}
				catch (TaskCanceledException)
				{
					await DequeueAllCoinsFromMixNoLockAsync(reason);
				}
			}
		}

		private async Task DequeueAllCoinsFromMixNoLockAsync(string reason)
		{
			await DequeueCoinsFromMixNoLockAsync(State.GetAllQueuedCoins().ToArray(), reason);
		}

		private async Task DequeueSpentCoinsFromMixNoLockAsync()
		{
			await DequeueCoinsFromMixNoLockAsync(State.GetSpentCoins().ToArray());
		}

		private async Task DequeueCoinsFromMixNoLockAsync(TxoRef coin, string reason = null)
		{
			await DequeueCoinsFromMixNoLockAsync(new[] { coin }, reason);
		}

		private async Task DequeueCoinsFromMixNoLockAsync(TxoRef[] coins, string reason = null)
		{
			if (coins is null || !coins.Any())
			{
				return;
			}

			List<Exception> exceptions = new List<Exception>();

			foreach (var coinReference in coins)
			{
				var coinToDequeue = State.GetSingleOrDefaultCoin(coinReference);
				if (coinToDequeue is null)
				{
					continue;
				}

				foreach (long roundId in State.GetPassivelyMixingRounds())
				{
					var round = State.GetSingleOrDefaultRound(roundId);
					if (round is null)
					{
						throw new NotSupportedException("This is impossible.");
					}

					if (round.CoinsRegistered.Contains(coinToDequeue))
					{
						try
						{
							await round.Registration.AliceClient.PostUnConfirmationAsync(); // AliceUniqueId must be there.
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
					if (round is null)
					{
						continue;
					}

					if (round.CoinsRegistered.Contains(coinToDequeue))
					{
						if (!coinToDequeue.Unspent) // If coin was spent, well that sucks, except if it was spent by the tumbler in signing phase.
						{
							State.ClearRoundRegistration(round.State.RoundId);
							continue;
						}
						else
						{
							exceptions.Add(new NotSupportedException($"Cannot deque coin in {round.State.Phase} phase. Coin: {coinToDequeue.Index}:{coinToDequeue.TransactionId}."));
						}
					}
				}

				SmartCoin coinWaitingForMix = State.GetSingleOrDefaultFromWaitingList(coinToDequeue);
				if (coinWaitingForMix != null) // If it is not being mixed, we can just remove it.
				{
					RemoveCoin(coinWaitingForMix, reason);
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

		private void RemoveCoin(SmartCoin coinWaitingForMix, string reason = null)
		{
			State.RemoveCoinFromWaitingList(coinWaitingForMix);
			coinWaitingForMix.CoinJoinInProgress = false;
			coinWaitingForMix.Secret = null;
			if (coinWaitingForMix.Label == "ZeroLink Change" && coinWaitingForMix.Unspent)
			{
				coinWaitingForMix.Label = "ZeroLink Dequeued Change";
				var key = KeyManager.GetKeys(x => x.P2wpkhScript == coinWaitingForMix.ScriptPubKey).SingleOrDefault();
				if (key != null)
				{
					key.SetLabel(coinWaitingForMix.Label, KeyManager);
				}
			}
			CoinDequeued?.Invoke(this, coinWaitingForMix);
			var correctReason = Guard.Correct(reason);
			var reasonText = correctReason != "" ? $" Reason: {correctReason}" : "";
			Logger.LogInfo<CcjClient>($"Coin dequeued: {coinWaitingForMix.Index}:{coinWaitingForMix.TransactionId}.{reasonText}");
		}

		public async Task StopAsync()
		{
			Synchronizer.ResponseArrived -= Synchronizer_ResponseArrivedAsync;

			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Cancel?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}

			Cancel?.Dispose();
			Cancel = null;

			using (await MixLock.LockAsync())
			{
				await DequeueSpentCoinsFromMixNoLockAsync();

				State.DisposeAllAliceClients();

				IEnumerable<TxoRef> allCoins = State.GetAllQueuedCoins();
				foreach (var coinReference in allCoins)
				{
					try
					{
						var coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
						if (coin is null)
						{
							continue; // The coin is not present anymore. Good. This should never happen though.
						}
						await DequeueCoinsFromMixNoLockAsync(coin.GetTxoRef(), "Stopping Wasabi.");
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
