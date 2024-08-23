using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests;

public interface IWabiSabiStatusApiRequestHandler
{
	Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken);
}

public interface IWabiSabiApiRequestHandler : IWabiSabiStatusApiRequestHandler
{
	Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken);

	Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken);

	Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken);

	Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken);

	Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken);

	Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken);

	Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken);
}

public class NullWabiSabiStatusApiRequestHandler : IWabiSabiStatusApiRequestHandler
{
	public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
	{
		return Task.FromResult(new RoundStateResponse([], []));
	}
}
