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
				Dictionary<Coin, byte[]> coinRoundSignaturePairs = await InputRegistrationHandler.PreProcessAsync(request, Prison, Rpc, Config).ConfigureAwait(false);

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
