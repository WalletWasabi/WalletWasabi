using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To make batched requests.
	/// </summary>
	[Produces("application/json")]
	[ApiController]
	[Route("[controller]")]
	public class ArenaController : ControllerBase
	{
		private IArenaRequestHandler handler;

		public ArenaController(IArenaRequestHandler handler)
		{
			this.handler = handler;
		}

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request)
		{
			return handler.ConfirmConnectionAsync(request);
		}

		[HttpPost("registerinput")]

		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request)
		{
			return handler.RegisterInputAsync(request);
		}

		[HttpPost("registeroutput")]
		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request)
		{
			return handler.RegisterOutputAsync(request);
		}

		[HttpPost("reissuecredential")]
		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request)
		{
			return handler.ReissueCredentialAsync(request);
		}

		[HttpPost("removeimput")]
		public Task RemoveInputAsync(InputsRemovalRequest request)
		{
			return handler.RemoveInputAsync(request);
		}

		[HttpPost("signtransaction")]
		public Task SignTransactionAsync(TransactionSignaturesRequest request)
		{
			return handler.SignTransactionAsync(request);
		}
	}
}
