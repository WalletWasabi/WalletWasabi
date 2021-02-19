using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public class PostRequestHandler : IAsyncDisposable
	{
		public PostRequestHandler(WabiSabiConfig config, Prison prison, Arena arena, IRPCClient rpc)
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
		public Arena Arena { get; }
		public IRPCClient Rpc { get; }
		public Network Network { get; }

		public async Task<InputsRegistrationResponse> RegisterInputAsync(InputsRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				var inputRoundSignaturePairs = request.InputRoundSignaturePairs;
				var inputs = inputRoundSignaturePairs.Select(x => x.Input);

				int inputCount = inputs.Count();
				if (inputCount != inputs.Distinct().Count())
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonUniqueInputs);
				}
				if (inputs.Any(x => Prison.TryGet(x, out var inmate) && (!Config.AllowNotedInputRegistration || inmate.Punishment != Punishment.Noted)))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputBanned);
				}

				Dictionary<Coin, byte[]> coinRoundSignaturePairs = new();
				foreach (var inputRoundSignaturePair in inputRoundSignaturePairs)
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
					if (!txOutResponse.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
					}

					coinRoundSignaturePairs.Add(new Coin(input, txOutResponse.TxOut), inputRoundSignaturePair.RoundSignature);
				}

				return Arena.RegisterInput(
					request.RoundId,
					coinRoundSignaturePairs,
					request.ZeroAmountCredentialRequests,
					request.ZeroWeightCredentialRequests);
			}
		}

		public void RemoveInput(InputsRemovalRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				Arena.RemoveInput(request);
			}
		}

		public ConnectionConfirmationResponse ConfirmConnection(ConnectionConfirmationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return Arena.ConfirmConnection(request);
			}
		}

		public OutputRegistrationResponse RegisterOutput(OutputRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				if (!request.Script.IsScriptType(ScriptType.P2WPKH))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
				}

				return Arena.RegisterOutput(request);
			}
		}

		public void SignTransaction(TransactionSignaturesRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				Arena.SignTransaction(request);
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
