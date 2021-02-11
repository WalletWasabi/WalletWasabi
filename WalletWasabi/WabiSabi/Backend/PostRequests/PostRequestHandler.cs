using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public class PostRequestHandler : IAsyncDisposable
	{
		public PostRequestHandler(WabiSabiConfig config, Prison prison, IArena arena, IRPCClient rpc)
		{
			Config = config;
			Prison = prison;
			Arena = arena;
			Rpc = rpc;
			Network = rpc.Network;
		}

		private bool DisposeStarted { get; set; } = false;
		private object DisposeStartedLock { get; } = new();
		private AbandonedTasks RunningRequests { get; } = new();
		public WabiSabiConfig Config { get; }
		public Prison Prison { get; }
		public IArena Arena { get; }
		public IRPCClient Rpc { get; }
		public Network Network { get; }

		public async Task<InputsRegistrationResponse> RegisterInputAsync(InputsRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				if (!Arena.TryGetRound(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}
				if (round.Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				if (Config.MaxInputCountByAlice < request.InputRoundSignaturePairs.Count())
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooManyInputs);
				}

				foreach (var inputRoundSignaturePair in request.InputRoundSignaturePairs)
				{
					OutPoint input = inputRoundSignaturePair.Input;
					var txOutResponse = await Rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true).ConfigureAwait(false);
					if (txOutResponse is null)
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputSpent);
					}
					if (txOutResponse.Confirmations == 0)
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputUnconfirmed);
					}
					if (txOutResponse.IsCoinBase && txOutResponse.Confirmations <= 100)
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputImmature);
					}
					if (!Config.AllowedScriptTypes.Contains(txOutResponse.ScriptPubKeyType))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputScriptNotAllowed);
					}
					var address = (BitcoinWitPubKeyAddress)txOutResponse.TxOut.ScriptPubKey.GetDestinationAddress(Network);
					if (!address.VerifyMessage(round.Hash, inputRoundSignaturePair.RoundSignature))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongRoundSignature);
					}
				}

				throw new NotImplementedException();
			}
		}

		public void RemoveInput(InputsRemovalRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				throw new NotImplementedException();
			}
		}

		public ConnectionConfirmationResponse ConfirmConnection(ConnectionConfirmationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				throw new NotImplementedException();
			}
		}

		public OutputRegistrationResponse RegisterOutput(OutputRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				throw new NotImplementedException();
			}
		}

		public void SignTransaction(TransactionSignaturesRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				throw new NotImplementedException();
			}
		}

		private void DisposeGuard()
		{
			lock (DisposeStartedLock)
			{
				if (DisposeStarted)
				{
					throw new ObjectDisposedException(nameof(PostRequestHandler));
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			lock (DisposeStartedLock)
			{
				DisposeStarted = true;
			}
			await RunningRequests.WhenAllAsync().ConfigureAwait(false);
		}
	}
}
