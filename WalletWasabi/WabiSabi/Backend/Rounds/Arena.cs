using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
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
		}

		public Dictionary<Guid, Round> Rounds { get; } = new();
		private AsyncLock AsyncLock { get; } = new();
		public Network Network { get; }
		public WabiSabiConfig Config { get; }
		public IRPCClient Rpc { get; }
		public Prison Prison { get; }
		public SecureRandom Random { get; }

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
			{
				// Remove timed out alices.
				TimeoutAlices();

				await StepTransactionSigningPhaseAsync().ConfigureAwait(false);

				StepOutputRegistrationPhase();

				StepConnectionConfirmationPhase();

				StepInputRegistrationPhase();

				cancel.ThrowIfCancellationRequested();

				// Ensure there's at least one non-blame round in input registration.
				await CreateRoundsAsync().ConfigureAwait(false);
			}
		}

		private void StepInputRegistrationPhase()
		{
			foreach (var round in Rounds.Values.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.GetInputRegistrationTimeout(x)))
				.ToArray())
			{
				if (round.InputCount < Config.MinInputCountByRound)
				{
					Rounds.Remove(round.Id);
				}
				else
				{
					round.SetPhase(Phase.ConnectionConfirmation);
				}
			}
		}

		private void StepConnectionConfirmationPhase()
		{
			foreach (var round in Rounds.Values.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
			{
				if (round.Alices.All(x => x.ConfirmedConnetion))
				{
					round.SetPhase(Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationStart + round.ConnectionConfirmationTimeout < DateTimeOffset.UtcNow)
				{
					var alicesDidntConfirm = round.Alices.Where(x => !x.ConfirmedConnetion).ToArray();
					foreach (var alice in alicesDidntConfirm)
					{
						Prison.Note(alice, round.Id);
					}
					round.Alices.RemoveAll(x => alicesDidntConfirm.Contains(x));

					if (round.InputCount < Config.MinInputCountByRound)
					{
						Rounds.Remove(round.Id);
					}
					else
					{
						round.SetPhase(Phase.OutputRegistration);
					}
				}
			}
		}

		private void StepOutputRegistrationPhase()
		{
			foreach (var round in Rounds.Values.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
			{
				long aliceSum = round.Alices.Sum(x => x.CalculateRemainingAmountCredentials(round.FeeRate));
				long bobSum = round.Bobs.Sum(x => x.CredentialAmount);
				var diff = aliceSum - bobSum;
				if (diff == 0 || round.OutputRegistrationStart + round.OutputRegistrationTimeout < DateTimeOffset.UtcNow)
				{
					// Build a coinjoin:
					var coinjoin = round.Coinjoin;

					// Add inputs:
					var spentCoins = round.Alices.SelectMany(x => x.Coins).ToArray();
					foreach (var input in spentCoins.Select(x => x.Outpoint))
					{
						coinjoin.Inputs.Add(input);
					}

					// Add outputs:
					foreach (var bob in round.Bobs)
					{
						coinjoin.Outputs.AddWithOptimize(bob.CalculateOutputAmount(round.FeeRate), bob.Script);
					}

					// Shuffle & sort:
					// This is basically just decoration.
					coinjoin.Inputs.Shuffle();
					coinjoin.Outputs.Shuffle();
					coinjoin.Inputs.SortByAmount(spentCoins);
					coinjoin.Outputs.SortByAmount();

					// If timeout we must fill up the outputs to build a reasonable transaction.
					// This won't be signed by the alice who failed to provide output, so we know who to ban.
					if (diff > round.MinRegistrableAmount)
					{
						var diffMoney = Money.Satoshis(diff);
						coinjoin.Outputs.AddWithOptimize(diffMoney, Config.BlameScript);
					}

					round.EncryptedCoinjoin = StringCipher.Encrypt(coinjoin.ToHex(), round.UnsignedTxSecret);

					round.SetPhase(Phase.TransactionSigning);
				}
			}
		}

		private async Task StepTransactionSigningPhaseAsync()
		{
			foreach (var round in Rounds.Values.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
			{
				var coinjoin = round.Coinjoin;
				var isFullySigned = coinjoin.Inputs.All(x => x.HasWitScript());
				if (isFullySigned)
				{
					try
					{
						await Rpc.SendRawTransactionAsync(coinjoin).ConfigureAwait(false);
						round.SetPhase(Phase.TransactionBroadcasting);
					}
					catch (Exception ex)
					{
						Logger.LogWarning(ex);
						await FailTransactionSigningPhaseAsync(round).ConfigureAwait(false);
					}
				}
				else if (round.TransactionSigningStart + round.TransactionSigningTimeout < DateTimeOffset.UtcNow)
				{
					await FailTransactionSigningPhaseAsync(round).ConfigureAwait(false);
				}
			}
		}

		private async Task FailTransactionSigningPhaseAsync(Round round)
		{
			var alicesWhoDidntSign = round
				.Alices
				.Where(x => !x
					.Coins
					.Select(x => x.Outpoint)
					.All(y => round.Coinjoin.Inputs.Where(x => x.HasWitScript()).Select(x => x.PrevOut).Contains(y)))
				.ToHashSet();

			foreach (var alice in alicesWhoDidntSign)
			{
				Prison.Note(alice, round.Id);
			}

			round.Alices.RemoveAll(x => alicesWhoDidntSign.Contains(x));
			Rounds.Remove(round.Id);

			if (round.InputCount >= Config.MinInputCountByRound)
			{
				await CreateBlameRoundAsync(round).ConfigureAwait(false);
			}
		}

		private async Task CreateBlameRoundAsync(Round round)
		{
			var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = new(Config, Network, Random, feeRate, blameOf: round);
			Round blameRound = new(parameters);
			Rounds.Add(blameRound.Id, blameRound);
		}

		private async Task CreateRoundsAsync()
		{
			if (!Rounds.Values.Any(x => !x.IsBlameRound && x.Phase == Phase.InputRegistration))
			{
				var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.ConfirmationTarget, EstimateSmartFeeMode.Conservative).ConfigureAwait(false)).FeeRate;

				RoundParameters roundParams = new(Config, Network, Random, feeRate);
				Round r = new(roundParams);
				Rounds.Add(r.Id, r);
			}
		}

		private void TimeoutAlices()
		{
			foreach (var round in Rounds.Values.Where(x => !x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.GetInputRegistrationTimeout(x))).ToArray())
			{
				var removedAliceCount = round.Alices.RemoveAll(x => x.Deadline < DateTimeOffset.UtcNow);
				if (removedAliceCount > 0)
				{
					Logger.LogInfo($"{removedAliceCount} alices timed out and removed.");
				}
			}
		}

		public async Task<InputsRegistrationResponse> RegisterInputAsync(
			Guid roundId,
			IDictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				return InputRegistrationHandler.RegisterInput(
					Config,
					roundId,
					coinRoundSignaturePairs,
					zeroAmountCredentialRequests,
					zeroWeightCredentialRequests,
					Rounds,
					Network);
			}
		}

		public async Task RemoveInputAsync(InputsRemovalRequest request)
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}
				if (round.Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				round.Alices.RemoveAll(x => x.Id == request.AliceId);
			}
		}

		public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request)
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				var alice = round.Alices.FirstOrDefault(x => x.Id == request.AliceId);
				if (alice is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound);
				}

				var amountZeroCredentialResponse = round.AmountCredentialIssuer.HandleRequest(request.ZeroAmountCredentialRequests);
				var weightZeroCredentialResponse = round.WeightCredentialIssuer.HandleRequest(request.ZeroWeightCredentialRequests);

				var realAmountCredentialRequests = request.RealAmountCredentialRequests;
				var realWeightCredentialRequests = request.RealWeightCredentialRequests;

				if (realWeightCredentialRequests.Delta != alice.CalculateRemainingWeightCredentials(round.RegistrableWeightCredentials))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
				}
				if (realAmountCredentialRequests.Delta != alice.CalculateRemainingAmountCredentials(round.FeeRate))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials);
				}

				if (round.Phase == Phase.InputRegistration)
				{
					alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
					return new(
						amountZeroCredentialResponse,
						weightZeroCredentialResponse);
				}
				else if (round.Phase == Phase.ConnectionConfirmation)
				{
					var amountRealCredentialResponse = round.AmountCredentialIssuer.HandleRequest(realAmountCredentialRequests);
					var weightRealCredentialResponse = round.WeightCredentialIssuer.HandleRequest(realWeightCredentialRequests);
					alice.ConfirmedConnetion = true;

					return new(
						amountZeroCredentialResponse,
						weightZeroCredentialResponse,
						amountRealCredentialResponse,
						weightRealCredentialResponse);
				}
				else
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
			}
		}

		public async Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request)
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				var credentialAmount = -request.AmountCredentialRequests.Delta;

				Bob bob = new(request.Script, credentialAmount);

				var outputValue = bob.CalculateOutputAmount(round.FeeRate);
				if (outputValue < round.MinRegistrableAmount)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
				}
				if (outputValue > round.MaxRegistrableAmount)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
				}

				var weightCredentialRequests = request.WeightCredentialRequests;
				if (-weightCredentialRequests.Delta != bob.CalculateWeight())
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
				}

				if (round.Phase != Phase.OutputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				var amountCredentialResponse = round.AmountCredentialIssuer.HandleRequest(request.AmountCredentialRequests);
				var weightCredentialResponse = round.WeightCredentialIssuer.HandleRequest(weightCredentialRequests);

				round.Bobs.Add(bob);

				return new(
					round.UnsignedTxSecret,
					amountCredentialResponse,
					weightCredentialResponse);
			}
		}

		public async Task SignTransactionAsync(TransactionSignaturesRequest request)
		{
			using (await AsyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				if (round.Phase != Phase.TransactionSigning)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				foreach (var inputWitnessPair in request.InputWitnessPairs)
				{
					var index = (int)inputWitnessPair.InputIndex;
					var witness = inputWitnessPair.Witness;

					// If input is already signed, don't bother.
					if (round.Coinjoin.Inputs[index].HasWitScript())
					{
						continue;
					}

					// Verify witness.
					// 1. Copy UnsignedCoinJoin.
					Transaction cjCopy = Transaction.Parse(round.Coinjoin.ToHex(), Network);

					// 2. Sign the copy.
					cjCopy.Inputs[index].WitScript = witness;

					// 3. Convert the current input to IndexedTxIn.
					IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();

					// 4. Find the corresponding registered input.
					Coin registeredCoin = round.Alices.SelectMany(x => x.Coins).Single(x => x.Outpoint == cjCopy.Inputs[index].PrevOut);

					// 5. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
					if (!currentIndexedInput.VerifyScript(registeredCoin, out ScriptError error))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongCoinjoinSignature);
					}

					// Finally add it to our CJ.
					round.Coinjoin.Inputs[index].WitScript = witness;
				}
			}
		}

		public override void Dispose()
		{
			Random.Dispose();
			base.Dispose();
		}
	}
}
