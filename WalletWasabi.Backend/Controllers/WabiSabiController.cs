using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	[Produces("application/json")]
	[ApiController]
	[Route("[controller]")]
	public class WabiSabiController : ControllerBase
	{
		private IArenaRequestHandler handler;

		public WabiSabiController(IArenaRequestHandler handler)
		{
			this.handler = handler;
		}

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request)
		{
			return handler.ConfirmConnectionAsync(request);
		}

		[HttpPost("input-registration")]
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request)
		{
			return handler.RegisterInputAsync(request);
		}

		[HttpPost("output-registration")]
		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request)
		{
			return handler.RegisterOutputAsync(request);
		}

		[HttpPost("credential-issuance")]
		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request)
		{
			return handler.ReissueCredentialAsync(request);
		}

		[HttpPost("input-unregistration")]
		public Task RemoveInputAsync(InputsRemovalRequest request)
		{
			return handler.RemoveInputAsync(request);
		}

		[HttpPost("transaction-signature")]
		public Task SignTransactionAsync(TransactionSignaturesRequest request)
		{
			return handler.SignTransactionAsync(request);
		}
	}
}
