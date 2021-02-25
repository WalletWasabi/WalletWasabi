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

				StepConnectionConfirmationPhase();

				StepInputRegistrationPhase();

				// Ensure there's at least one non-blame round in input registration.
				await CreateRoundsAsync().ConfigureAwait(false);
			}
		}

		private void StepInputRegistrationPhase()
		{
			foreach (var round in Rounds.Values.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.InputRegistrationTimeout)))
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
			foreach (var round in Rounds.Values.Where(x => x.Phase == Phase.ConnectionConfirmation))
			{
				if (round.Alices.All(x => x.ConfirmedConnetion))
				{
					round.SetPhase(Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationStart + Config.ConnectionConfirmationTimeout < DateTimeOffset.UtcNow)
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
			foreach (var round in Rounds.Values.Where(x => !x.IsInputRegistrationEnded(Config.MaxInputCountByRound, Config.InputRegistrationTimeout)))
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
					roundId,
					coinRoundSignaturePairs,
					zeroAmountCredentialRequests,
					zeroWeightCredentialRequests,
					Rounds,
					Network,
					Config.MaxInputCountByRound,
					Config.InputRegistrationTimeout);
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

				var bob = new Bob(request.Script, credentialAmount);

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
