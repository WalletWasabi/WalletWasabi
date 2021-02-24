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

				return await Arena.RegisterInputAsync(
					request.RoundId,
					coinRoundSignaturePairs,
					request.ZeroAmountCredentialRequests,
					request.ZeroWeightCredentialRequests).ConfigureAwait(false);
			}
		}

		public async Task RemoveInputAsync(InputsRemovalRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.RemoveInputAsync(request).ConfigureAwait(false);
			}
		}

		public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.ConfirmConnectionAsync(request).ConfigureAwait(false);
			}
		}

		public async Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				if (!request.Script.IsScriptType(ScriptType.P2WPKH))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
				}

				return await Arena.RegisterOutputAsync(request).ConfigureAwait(false);
			}
		}

		public async Task SignTransactionAsync(TransactionSignaturesRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.SignTransactionAsync(request).ConfigureAwait(false);
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
