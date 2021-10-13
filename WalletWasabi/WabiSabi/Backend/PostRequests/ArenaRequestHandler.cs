using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public class ArenaRequestHandler : IAsyncDisposable
	{
		public ArenaRequestHandler(WabiSabiConfig config, Prison prison, Arena arena)
		{
			Config = config;
			Prison = prison;
			Arena = arena;
		}

		private bool DisposeStarted { get; set; } = false;
		private object DisposeStartedLock { get; } = new();
		private AbandonedTasks RunningRequests { get; } = new();
		public WabiSabiConfig Config { get; }
		public Prison Prison { get; }
		public Arena Arena { get; }

		public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.RegisterInputAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.RemoveInputAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.ConfirmConnectionAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.RegisterOutputAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.SignTransactionAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.ReissuanceAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				await Arena.ReadyToSignAsync(request, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task<RoundState[]> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
		{
			DisposeGuard();
			using (RunningTasks.RememberWith(RunningRequests))
			{
				return await Arena.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);
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
	}
}
