using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Arena : PeriodicRunner
	{
		public Arena(TimeSpan period) : base(period)
		{
		}

		public Dictionary<Guid, Round> Rounds { get; } = new();
		private object Lock { get; } = new();

		public bool TryGetRound(Guid roundId, [NotNullWhen(true)] out Round? round)
		{
			lock (Lock)
			{
				return Rounds.TryGetValue(roundId, out round);
			}
		}

		protected override Task ActionAsync(CancellationToken cancel)
		{
			return Task.CompletedTask;
		}

		public InputsRegistrationResponse RegisterInput(
			Guid roundId,
			IDictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			if (!TryGetRound(roundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			return round.RegisterAlice(
				new Alice(coinRoundSignaturePairs),
				zeroAmountCredentialRequests,
				zeroWeightCredentialRequests);
		}

		public void RemoveInput(InputsRemovalRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}
			round.RemoveAlice(request.AliceId);
		}

		public ConnectionConfirmationResponse ConfirmConnection(ConnectionConfirmationRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			return round.ConfirmAlice(request);
		}

		public OutputRegistrationResponse RegisterOutput(OutputRegistrationRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			var credentialAmount = -request.AmountCredentialRequests.Delta;
			return round.RegisterBob(
				new Bob(
					request.Script,
					credentialAmount),
					request.AmountCredentialRequests,
					request.WeightCredentialRequests);
		}

		public void SignTransaction(TransactionSignaturesRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}
			round.SubmitTransactionSignatures(request.InputWitnessPairs);
		}
	}
}
