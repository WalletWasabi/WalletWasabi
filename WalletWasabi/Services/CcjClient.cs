using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
using WalletWasabi.WebClients.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class CcjClient
	{
		public Network Network { get; }
		BlindingRsaPubKey CoordinatorPubKey { get; }
		public KeyManager KeyManager { get; }

		public AliceClient AliceClient { get; }
		public BobClient BobClient { get; }
		public SatoshiClient SatoshiClient { get; }

		private AsyncLock MixLock { get; }

		public CcjClientState State { get; }

		private long _frequentStatusProcessingIfNotMixing;

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Stop { get; }

		public CcjClient(Network network, BlindingRsaPubKey coordinatorPubKey, KeyManager keyManager, Uri ccjHostUri, IPEndPoint torSocks5EndPoint = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			CoordinatorPubKey = Guard.NotNull(nameof(coordinatorPubKey), coordinatorPubKey);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			AliceClient = new AliceClient(ccjHostUri, torSocks5EndPoint);
			BobClient = new BobClient(ccjHostUri, torSocks5EndPoint);
			SatoshiClient = new SatoshiClient(ccjHostUri, torSocks5EndPoint);

			_running = 0;
			Stop = new CancellationTokenSource();
			_frequentStatusProcessingIfNotMixing = 0;
			State = new CcjClientState();
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
							int delay;
							using (await MixLock.LockAsync())
							{
								// If stop was requested return.
								if (IsRunning == false) return;

								// if mixing >= connConf: delay = new Random().Next(2, 7);
								if (State.GetActivelyMixingRounds().Count() > 0)
								{
									delay = new Random().Next(2, 7);
								}
								else if (Interlocked.Read(ref _frequentStatusProcessingIfNotMixing) == 1 || State.GetPassivelyMixingRounds().Count() > 0)
								{
									double rand = double.Parse($"0.{new Random().Next(2, 8)}"); // randomly between every 0.2 * connConfTimeout - 7 and 0.8 * connConfTimeout
									delay = Math.Max(0, (int)(rand * State.GetSmallestRegistrationTimeout() - 7));
								}
								else
								{
									// dormant
									await Task.Delay(1000); // dormant
									continue;
								}
							}

							await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);
							await ProcessStatusAsync();
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
				IEnumerable<CcjRunningRoundState> states;
				int delay;
				using (await MixLock.LockAsync())
				{
					states = await SatoshiClient.GetAllRoundStatesAsync();
					State.UpdateRoundsByStates(states.ToArray());
					delay = new Random().Next(0, 7); // delay the response to defend timing attack privacy
				}

				await Task.Delay(TimeSpan.FromSeconds(delay), Stop.Token);

				using (await MixLock.LockAsync())
				{
					State.RemoveSpentCoinsFromWaitingList(); // Make sure coins those were somehow spent are removed.

					CcjClientRound inputRegistrableRound = State.GetRegistrableRound();
					if (inputRegistrableRound.AliceUniqueId == null) // If didn't register already, check what can we register.
					{
						try
						{
							IEnumerable<SmartCoin> registrableCoins = State.GetRegistrableCoins(
								inputRegistrableRound.State.MaximumInputCountPerPeer,
								inputRegistrableRound.State.Denomination,
								inputRegistrableRound.State.FeePerInputs,
								inputRegistrableRound.State.FeePerOutputs);

							if (registrableCoins.Count() > 0)
							{
								var changeKey = KeyManager.GenerateNewKey("CoinJoin Change Output", KeyState.Locked, isInternal: true);
								var activeKey = KeyManager.GenerateNewKey("CoinJoin Active Output", KeyState.Locked, isInternal: true);
								var blind = CoordinatorPubKey.Blind(activeKey.GetP2wpkhScript().ToBytes());

								var inputProofs = new List<InputProofModel>();
								foreach (SmartCoin coin in registrableCoins)
								{
									var inputProof = new InputProofModel
									{
										Input = coin.GetOutPoint(),
										Proof = coin.Secret.PrivateKey.SignMessage(ByteHelpers.ToHex(blind.BlindedData))
									};
									inputProofs.Add(inputProof);
								}
								InputsResponse inputsResponse = await AliceClient.PostInputsAsync(changeKey.GetP2wpkhScript(), blind.BlindedData, inputProofs.ToArray());

								byte[] unblindedSignature = CoordinatorPubKey.UnblindSignature(inputsResponse.BlindedOutputSignature, blind.BlindingFactor);

								if (!CoordinatorPubKey.Verify(unblindedSignature, activeKey.GetP2wpkhScript().ToBytes()))
								{
									throw new NotSupportedException("Coordinator did not sign the blinded output properly.");
								}

								CcjClientRound roundRegistered = State.GetSingleOrDefaultRound(inputsResponse.RoundId);
								if (roundRegistered == null)
								{
									// If our SatoshiClient doesn't yet know about the round because of the dealy create it.
									// Make its state as it'd be the same as our assumed round was, except the roundId and registeredPeerCount, it'll be updated later.
									roundRegistered = new CcjClientRound(CcjRunningRoundState.CloneExcept(inputRegistrableRound.State, inputsResponse.RoundId, registeredPeerCount: 1));
									State.AddOrReplaceRound(roundRegistered);
								}

								foreach (var coin in registrableCoins)
								{
									roundRegistered.CoinsRegistered.Add(coin);
									State.RemoveCoinFromWaitingList(coin);
								}
								roundRegistered.ActiveOutput = activeKey;
								roundRegistered.ChangeOutput = changeKey;								
								roundRegistered.UnblindedSignature = unblindedSignature;
								roundRegistered.AliceUniqueId = inputsResponse.UniqueId;
							}
						}
						catch (Exception ex)
						{
							Logger.LogError<CcjClient>(ex);
						}
					}
					else // We registered, let's confirm we're online.
					{
						try
						{
							string roundHash = await AliceClient.PostConfirmationAsync(inputRegistrableRound.State.RoundId, (Guid)inputRegistrableRound.AliceUniqueId);
							if (roundHash != null) // Then the phase went to connection confirmation. 
							{
								inputRegistrableRound.RoundHash = roundHash;
								inputRegistrableRound.State.Phase = CcjRoundPhase.ConnectionConfirmation;
							}
						}
						catch (Exception ex)
						{
							if(ex.Message.StartsWith("NotFound", StringComparison.Ordinal)) // Alice timed out.
							{
								State.ClearRoundRegistration(inputRegistrableRound.State.RoundId);
							}
							Logger.LogError<CcjClient>(ex);
						}
					}

					foreach (CcjClientRound ongoingRound in State.GetActivelyMixingRounds())
					{
						try
						{
							if (ongoingRound.State.Phase == CcjRoundPhase.ConnectionConfirmation)
							{
								if (ongoingRound.RoundHash == null) // If we didn't already obtained our roundHash obtain it.
								{
									string roundHash = await AliceClient.PostConfirmationAsync(inputRegistrableRound.State.RoundId, (Guid)inputRegistrableRound.AliceUniqueId);
									if (roundHash == null)
									{
										throw new NotSupportedException("Coordinator didn't gave us the expected roundHash, even though it's in ConnectionConfirmation phase.");
									}
									else
									{
										ongoingRound.RoundHash = roundHash;
									}
								}
							}
							else if (ongoingRound.State.Phase == CcjRoundPhase.OutputRegistration)
							{
								if (ongoingRound.RoundHash == null)
								{
									throw new NotSupportedException("Coordinator progressed to OutputRegistration phase, even though we didn't obtain roundHash.");
								}

								await BobClient.PostOutputAsync(ongoingRound.RoundHash, ongoingRound.ActiveOutput.GetP2wpkhScript(), ongoingRound.UnblindedSignature);
							}
							else if (ongoingRound.State.Phase == CcjRoundPhase.Signing)
							{
								Transaction unsignedCoinJoin = await AliceClient.GetUnsignedCoinJoinAsync(ongoingRound.State.RoundId, (Guid)ongoingRound.AliceUniqueId);
								if (NBitcoinHelpers.HashOutpoints(unsignedCoinJoin.Inputs.Select(x => x.PrevOut)) != ongoingRound.RoundHash)
								{
									throw new NotSupportedException("Coordinator provided invalid roundHash.");
								}
								Money amountBack = unsignedCoinJoin.Outputs
									.Where(x => x.ScriptPubKey == ongoingRound.ActiveOutput.GetP2wpkhScript() || x.ScriptPubKey == ongoingRound.ChangeOutput.GetP2wpkhScript())
									.Sum(y => y.Value);
								Money minAmountBack = ongoingRound.CoinsRegistered.Sum(x => x.Amount); // Start with input sum.
								minAmountBack -= ongoingRound.State.FeePerOutputs * 2 + ongoingRound.State.FeePerInputs * ongoingRound.CoinsRegistered.Count; // Minus miner fee.
								Money actualDenomination = unsignedCoinJoin.GetIndistinguishableOutputs().OrderByDescending(x => x.count).First().value; // Denomination may grow.
								Money expectedCoordinatorFee = new Money((ongoingRound.State.CoordinatorFeePercent * 0.01m) * decimal.Parse(actualDenomination.ToString(false, true)), MoneyUnit.BTC);
								minAmountBack -= expectedCoordinatorFee; // Minus expected coordinator fee.

								// If there's no change output then coordinator protection may happened:
								if (unsignedCoinJoin.Outputs.All(x => x.ScriptPubKey != ongoingRound.ChangeOutput.GetP2wpkhScript()))
								{
									Money minimumOutputAmount = new Money(0.0001m, MoneyUnit.BTC); // If the change would be less than about $1 then add it to the coordinator.
									Money onePercentOfDenomination = new Money(actualDenomination.ToDecimal(MoneyUnit.BTC) * 0.01m, MoneyUnit.BTC); // If the change is less than about 1% of the newDenomination then add it to the coordinator fee.
									Money minimumChangeAmount = Math.Max(minimumOutputAmount, onePercentOfDenomination);

									minAmountBack -= minimumChangeAmount; // Minus coordinator protections (so it won't create bad coinjoins.)
								}

								if (amountBack < minAmountBack)
								{
									throw new NotSupportedException("Coordinator did not add enough value to our outputs in the coinjoin.");
								}

								new TransactionBuilder()
									.AddKeys(ongoingRound.CoinsRegistered.Select(x => x.Secret).ToArray())
									.AddCoins(ongoingRound.CoinsRegistered.Select(x => x.GetCoin()))
									.SignTransactionInPlace(unsignedCoinJoin, SigHash.All);

								var myDic = new Dictionary<int, WitScript>();

								for (int i = 0; i < unsignedCoinJoin.Inputs.Count; i++)
								{
									var input = unsignedCoinJoin.Inputs[i];
									if (ongoingRound.CoinsRegistered.Select(x => x.GetOutPoint()).Contains(input.PrevOut))
									{
										myDic.Add(i, unsignedCoinJoin.Inputs[i].WitScript);
									}
								}

								await AliceClient.PostSignaturesAsync(ongoingRound.State.RoundId, (Guid)ongoingRound.AliceUniqueId, myDic);
							}
						}
						catch (Exception ex)
						{
							if (ex.Message.StartsWith("NotFound", StringComparison.Ordinal)) // Alice timed out.
							{
								State.ClearRoundRegistration(ongoingRound.State.RoundId);
							}
							Logger.LogError<CcjClient>(ex);
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

		public void ActivateFrequentStatusProcessing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 1);
		}

		public void DeactivateFrequentStatusProcessingIfNotMixing()
		{
			Interlocked.Exchange(ref _frequentStatusProcessingIfNotMixing, 0);
		}

		public IEnumerable<SmartCoin> QueueCoinsToMix(string password, params SmartCoin[] coins)
		{
			using (MixLock.Lock())
			{
				var successful = new List<SmartCoin>();

				foreach (SmartCoin coin in coins)
				{
					if (State.Contains(coin))
					{
						successful.Add(coin);
						continue;
					}

					if (coin.SpentOrLocked)
					{
						continue;
					}

					coin.Secret = KeyManager.GetSecrets(password, coin.ScriptPubKey).Single();
					
					coin.Locked = true;

					State.AddCoinToWaitingList(coin);
					successful.Add(coin);
				}

				return successful;
			}
		}
		
		public async Task DequeueCoinsFromMixAsync(params SmartCoin[] coins)
		{
			using (await MixLock.LockAsync())
			{
				List<Exception> exceptions = new List<Exception>();

				foreach (var coinToDequeue in coins)
				{
					foreach (var round in State.GetPassivelyMixingRounds())
					{
						if (round.CoinsRegistered.Contains(coinToDequeue))
						{
							try
							{
								await AliceClient.PostUnConfirmationAsync(round.State.RoundId, (Guid)round.AliceUniqueId); // AliceUniqueId must be there.
								State.ClearRoundRegistration(round.State.RoundId);
							}
							catch (Exception ex)
							{
								exceptions.Add(ex);
							}
						}
					}

					foreach (var round in State.GetActivelyMixingRounds())
					{
						if(round.CoinsRegistered.Contains(coinToDequeue))
						{
							exceptions.Add(new NotSupportedException($"Cannot deque coin in {round.State.Phase} phase. Coin: {coinToDequeue.Index}:{coinToDequeue.TransactionId}."));
						}
					}

					SmartCoin coinWaitingForMix = State.GetSingleOrDefaultFromWaitingList(coinToDequeue);
					if (coinWaitingForMix != null) // If it is not being mixed, we can just remove it.
					{
						State.RemoveCoinFromWaitingList(coinWaitingForMix);
						coinWaitingForMix.Locked = false;
						coinWaitingForMix.Secret = null;
					}
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
				await DequeueCoinsFromMixAsync(State.GetAllCoins().ToArray());
			}
			catch(Exception ex)
			{
				Logger.LogError<CcjClient>("Couldn't dequeue all coins. Some coins will likely be banned from mixing.");
				if (ex is AggregateException)
				{
					var aggrEx = ex as AggregateException;
					foreach(var innerEx in aggrEx.InnerExceptions)
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
