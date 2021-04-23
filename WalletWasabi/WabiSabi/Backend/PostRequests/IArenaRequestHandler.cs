using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public interface IArenaRequestHandler
	{
		Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request);

		Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request);

		Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request);

		Task RemoveInputAsync(InputsRemovalRequest request);

		Task SignTransactionAsync(TransactionSignaturesRequest request);

		Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request);
	}
}
