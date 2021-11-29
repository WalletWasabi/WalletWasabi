using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public interface IWabiSabiApiRequestHandler
	{
		Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken);

		Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken);

		Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken);

		Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken);

		Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken);

		Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken);

		Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken);

		Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken);
	}
}
