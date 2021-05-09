using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Backend.Filters;

namespace WalletWasabi.Backend.Controllers
{
	[ApiController]
	[ExceptionTranslate]
	[Route("[controller]")]
	[Produces("application/json")]
	public class WabiSabiController : ControllerBase, IArenaRequestHandler
	{
		private ArenaRequestHandler handler;

		public WabiSabiController(ArenaRequestHandler handler)
		{
			this.handler = handler;
		}

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellableToken)
		{
			return handler.ConfirmConnectionAsync(request, cancellableToken);
		}

		[HttpPost("input-registration")]
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellableToken)
		{
			return handler.RegisterInputAsync(request, cancellableToken);
		}

		[HttpPost("output-registration")]
		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellableToken)
		{
			return handler.RegisterOutputAsync(request, cancellableToken);
		}

		[HttpPost("credential-issuance")]
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

		[HttpGet("status")]
		public Task<RoundState[]> GetStatusAsync(CancellationToken cancellableToken)
		{
			return Task.FromResult(handler.Arena.Rounds.Values.Select(x => RoundState.FromRound(x)).ToArray());
		}
	}
}
