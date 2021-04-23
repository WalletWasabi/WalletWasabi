using NBitcoin;
using System;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public class ArenaRequestHandler : IAsyncDisposable, IArenaRequestHandler
	{
		public ArenaRequestHandler(WabiSabiConfig config, Prison prison, Arena arena, IRPCClient rpc)
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

		public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				var coin = await InputRegistrationHandler.OutpointToCoinAsync(request, Prison, Rpc, Config).ConfigureAwait(false);

				return await Arena.RegisterInputAsync(
					request.RoundId,
					coin,
					request.RoundSignature,
					request.ZeroAmountCredentialRequests,
					request.ZeroVsizeCredentialRequests).ConfigureAwait(false);
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
					throw new ObjectDisposedException(nameof(ArenaRequestHandler));
				}
			}
		}

		public async Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.ReissuanceAsync(request).ConfigureAwait(false);
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
