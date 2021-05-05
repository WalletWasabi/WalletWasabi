using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Filters;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	[Produces("application/json")]
	[ApiController]
	[Route("[controller]")]
	public class WabiSabiController : ControllerBase
	{
		private readonly IArenaRequestHandler handler;

		public WabiSabiController(IArenaRequestHandler handler)
		{
			this.handler = handler;
		}

		[HttpPost("connection-confirmation")]
		[IdempotentActionFilterAttribute]
		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellableToken)
		{
			return handler.ConfirmConnectionAsync(request, cancellableToken);
		}

		[HttpPost("input-registration")]
		[IdempotentActionFilterAttribute]
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellableToken)
		{
			return handler.RegisterInputAsync(request, cancellableToken);
		}

		[HttpPost("output-registration")]
		[IdempotentActionFilterAttribute]
		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellableToken)
		{
			return handler.RegisterOutputAsync(request, cancellableToken);
		}

		[HttpPost("credential-issuance")]
		[IdempotentActionFilterAttribute]
		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request, CancellationToken cancellableToken)
		{
			return handler.ReissueCredentialAsync(request, cancellableToken);
		}

		[HttpPost("input-unregistration")]
		public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
		{
			return handler.RemoveInputAsync(request, cancellableToken);
		}

		[HttpPost("transaction-signature")]
		public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
		{
			return handler.SignTransactionAsync(request, cancellableToken);
		}
	}
}
