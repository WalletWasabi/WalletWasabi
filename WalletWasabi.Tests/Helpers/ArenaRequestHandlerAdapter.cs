using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Tests.Helpers
{
	public class ArenaRequestHandlerAdapter : IWabiSabiApiRequestHandler
	{
		private readonly Arena arena;

		public ArenaRequestHandlerAdapter(Arena arena)
		{
			this.arena = arena;
		}

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
			=> arena.ConfirmConnectionAsync(request, cancellationToken);

		public Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
			=> arena.GetStatusAsync(cancellationToken);

		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
			=> arena.RegisterInputAsync(request, cancellationToken);

		public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
			=> arena.RegisterOutputAsync(request, cancellationToken);

		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
			=> arena.ReissuanceAsync(request, cancellationToken);

		public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
			=> arena.RemoveInputAsync(request, cancellationToken);

		public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
			=> arena.SignTransactionAsync(request, cancellationToken);

		public Task ReadyToSign(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
			=> arena.ReadyToSignAsync(request, cancellationToken);
	}
}
