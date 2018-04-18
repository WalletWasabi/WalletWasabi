using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To interact with the Chaumian CoinJoin Coordinator.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v1/btc/[controller]")]
	public class ChaumianCoinJoinController : Controller
	{
		private static RPCClient RpcClient => Global.RpcClient;

		private static Network Network => Global.Config.Network;

		private static CcjCoordinator Coordinator => Global.Coordinator;

		/// <summary>
		/// Satoshi gets various status information.
		/// </summary>
		/// <returns>CurrentPhase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, Version</returns>
		/// <response code="200">CurrentPhase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, Version</response>
		[HttpGet("status")]
		[ProducesResponseType(200)]
		public IActionResult GetStatus()
		{
			CcjRound round = Coordinator.GetCurrentRound();
			var response = new CcjStatusResponse
			{
				CurrentPhase = round.Phase,
				Denomination = round.Denomination,
				RegisteredPeerCount = round.CountAlices(syncronized: false),
				RequiredPeerCount = round.AnonymitySet,
				MaximumInputCountPerPeer = 7, // Constant for now. If we want to do something with it later, we'll put it to the config file.
				FeePerInputs = round.FeePerInputs,
				FeePerOutputs = round.FeePerOutputs,
				CoordinatorFeePercent = round.CoordinatorFeePercent,
				Version = 1
			};
			return Ok(response);
		}

		/// <summary>
		/// Alice asks for the final CoinJoin transaction.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <returns>The coinjoin Transaction.</returns>
		/// <response code="200">Returns the coinjoin transaction.</response>
		/// <response code="400">The provided uniqueId was malformed.</response>
		[HttpGet("coinjoin/{uniqueId}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		public IActionResult GetCoinJoin(string uniqueId)
		{
			CheckUniqueId(uniqueId, out IActionResult returnFailureResponse);
			if(returnFailureResponse != null)
			{
				return returnFailureResponse;
			}
			
			return Ok();
		}

		/// <summary>
		/// Alice must confirm her participation periodically in InputRegistration phase and confirm once in ConnectionConfirmation phase.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <returns>RoundHash if the phase is already ConnectionConfirmation.</returns>
		/// <response code="200">RoundHash if the phase is already ConnectionConfirmation.</response>
		/// <response code="204">If the phase is not ConnectionConfirmation.</response>
		/// <response code="400">The provided uniqueId was malformed.</response>
		[HttpPost("confirmation/{uniqueId}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		public IActionResult PostConfirmation(string uniqueId)
		{
			CheckUniqueId(uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			return Ok();
		}

		/// <summary>
		/// Alice can revoke her registration without penalty if the current phase is InputRegistration.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <response code="204">Alice sucessfully uncofirmed her participation.</response>
		/// <response code="400">The provided uniqueId was malformed.</response>
		[HttpPost("unconfirmation/{uniqueId}")]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		public IActionResult PostUncorfimation(string uniqueId)
		{
			CheckUniqueId(uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			return NoContent();
		}

		private void CheckUniqueId(string uniqueId, out IActionResult returnFailureResponse)
		{
			returnFailureResponse = null;
			if (string.IsNullOrWhiteSpace(uniqueId) || !ModelState.IsValid)
			{
				returnFailureResponse = BadRequest("Invalid uniqueId provided.");
			}

			Guid aliceGuid = Guid.Empty;
			try
			{
				aliceGuid = Guid.Parse(uniqueId);
			}
			catch (Exception ex)
			{
				Logger.LogDebug<ChaumianCoinJoinController>(ex);
				returnFailureResponse = BadRequest("Invalid uniqueId provided.");
			}
			if (aliceGuid == Guid.Empty) // Probably not possible
			{
				Logger.LogDebug<ChaumianCoinJoinController>($"Empty uniqueId GID provided in {nameof(GetCoinJoin)} function.");
				returnFailureResponse = BadRequest("Invalid uniqueId provided.");
			}
		}

		/// <summary>
		/// Alice registers her inputs.
		/// </summary>
		/// <returns>SignedBlindedOutput, UniqueId</returns>
		/// <response code="200">SignedBlindedOutput, UniqueId</response>
		[HttpPost("inputs")]
		[ProducesResponseType(200)]
		public IActionResult PostInputs()
		{
			return Ok();
		}

		/// <summary>
		/// Bob registers his output.
		/// </summary>
		[HttpPost("output")]
		[ProducesResponseType(204)]
		public IActionResult PostOutput()
		{
			return NoContent();
		}

		/// <summary>
		/// Alice posts her partial signatures.
		/// </summary>
		[HttpPost("signatures")]
		[ProducesResponseType(204)]
		public IActionResult PostSignatures()
		{
			return NoContent();
		}
	}
}