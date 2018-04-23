using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Crypto;
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

		private static BlindingRsaKey RsaKey => Global.RsaKey;
		
		/// <summary>
		/// Satoshi gets various status information.
		/// </summary>
		/// <returns>CurrentPhase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, Version</returns>
		/// <response code="200">CurrentPhase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, Version</response>
		[HttpGet("status")]
		[ProducesResponseType(200)]
		public IActionResult GetStatus()
		{
			CcjRound currentRound = Coordinator.GetCurrentRound();
			CcjRound currentInputRegistrationRound = Coordinator.GetCurrentInputRegisterableRound();
			var response = new CcjStatusResponse
			{
				CurrentPhase = currentRound.Phase,
				Denomination = currentInputRegistrationRound.Denomination,
				RegisteredPeerCount = currentInputRegistrationRound.CountAlices(syncronized: false),
				RequiredPeerCount = currentInputRegistrationRound.AnonymitySet,
				MaximumInputCountPerPeer = 7, // Constant for now. If we want to do something with it later, we'll put it to the config file.
				RegistrationTimeout = (int)currentInputRegistrationRound.AliceRegistrationTimeout.TotalSeconds,
				FeePerInputs = currentInputRegistrationRound.FeePerInputs,
				FeePerOutputs = currentInputRegistrationRound.FeePerOutputs,
				CoordinatorFeePercent = currentInputRegistrationRound.CoordinatorFeePercent,
				Version = 1
			};
			return Ok(response);
		}

		private static AsyncLock InputsLock { get; } = new AsyncLock();

		/// <summary>
		/// Alice registers her inputs.
		/// </summary>
		/// <returns>BlindedOutputSignature, UniqueId</returns>
		/// <response code="200">BlindedOutputSignature, UniqueId</response>
		/// <response code="400">If request is invalid.</response>
		/// <response code="503">If the round status changed while fulfilling the request.</response>
		[HttpPost("inputs")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(503)]
		public async Task<IActionResult> PostInputsAsync([FromBody]InputsRequest request)
		{
			// Validate request.
			if (!ModelState.IsValid
				|| request == null
				|| string.IsNullOrWhiteSpace(request.BlindedOutputHex)
				|| string.IsNullOrWhiteSpace(request.ChangeOutputScript)
				|| request.Inputs == null
				|| request.Inputs.Count() == 0
				|| request.Inputs.Any(x=> x.Input == null
					|| x.Input.Hash == null
					|| string.IsNullOrWhiteSpace(x.Proof)))
			{
				return BadRequest("Invalid request.");
			}

			if(request.Inputs.Count() > 7)
			{
				return BadRequest("Maximum 7 inputs can be registered.");
			}

			using (await InputsLock.LockAsync())
			{
				CcjRound round = Coordinator.GetCurrentInputRegisterableRound();

				// Do more checks.
				try
				{
					if (round.ContainsBlindedOutput(request.BlindedOutputHex, out List<Alice> _))
					{
						return BadRequest("Blinded output has already been registered.");
					}

					var changeOutput = new Script(request.ChangeOutputScript);
					
					var inputs = new HashSet<(OutPoint OutPoint, TxOut Output)>();

					var alicesToRemove = new HashSet<Guid>();

					foreach (InputProofModel inputProof in request.Inputs)
					{
						if (inputs.Any(x => x.OutPoint == inputProof.Input))
						{
							return BadRequest("Cannot register an input twice.");
						}
						if(round.ContainsInput(inputProof.Input, out List<Alice> tr))
						{
							alicesToRemove.UnionWith(tr.Select(x => x.UniqueId)); // Input is already registered by this alice, remove it later if all the checks are completed fine.
						}
						if (Coordinator.AnyRunningRoundContainsInput(inputProof.Input, out List<Alice> tnr))
						{
							if(tr.Union(tnr).Count() > tr.Count())
							{
								return BadRequest("Input is already registered in another round.");
							}
						}

						// ToDo: Refuse banned UTXO here!

						GetTxOutResponse getTxOutResponse = await RpcClient.GetTxOutAsync(inputProof.Input.Hash, (int)inputProof.Input.N, includeMempool: true);

						// Check if inputs are unspent.				
						if (getTxOutResponse == null)
						{
							return BadRequest("Provided input is not unspent.");
						}

						// ToDo: If unconfirmed, then handle the case if CoinJoin. (You can check for if the fee address in the output to decide if it was our CJ or not.)

						// Check if unconfirmed.
						if (getTxOutResponse.Confirmations == 0)
						{
							return BadRequest("Provided input is unconfirmed.");
						}

						// Check if immature.
						if (getTxOutResponse.Confirmations <= 100)
						{
							if (getTxOutResponse.IsCoinBase)
							{
								return BadRequest("Provided input is immature.");
							}
						}

						// Check if inputs are native segwit.
						if (getTxOutResponse.ScriptPubKeyType != "witness_v0_keyhash")
						{
							return BadRequest("Provided input must be witness_v0_keyhash.");
						}

						TxOut txout = getTxOutResponse.TxOut;

						var address = (BitcoinWitPubKeyAddress)txout.ScriptPubKey.GetDestinationAddress(Network);
						// Check if proofs are valid.
						bool validProof;
						try
						{							
							validProof = address.VerifyMessage(request.BlindedOutputHex, inputProof.Proof);							
						}
						catch (FormatException ex)
						{
							return BadRequest($"Provided proof is invalid: {ex.Message}");
						}
						if (!validProof)
						{
							return BadRequest("Provided proof is invalid.");
						}

						inputs.Add((inputProof.Input, txout));
					}

					// Check if inputs have enough coins.
					Money inputSum = inputs.Sum(x => x.Output.Value);
					Money networkFeeToPay = (inputs.Count() * round.FeePerInputs + 2 * round.FeePerOutputs);
					Money changeAmount = inputSum - (round.Denomination + networkFeeToPay);
					if (changeAmount < Money.Zero)
					{
						return BadRequest($"Not enough inputs are provided. Fee to pay: {networkFeeToPay.ToString(false, true)} BTC. Round denomination: {round.Denomination.ToString(false, true)} BTC. Only provided: {inputSum.ToString(false, true)} BTC.");
					}
					
					// Make sure Alice checks work.
					var alice = new Alice(inputs, networkFeeToPay, new Script(request.ChangeOutputScript), request.BlindedOutputHex);
					
					foreach (Guid aliceToRemove in alicesToRemove)
					{
						round.RemoveAlicesBy(aliceToRemove);
					}
					round.AddAlice(alice);

					// All checks are good. Sign.
					byte[] blindedData;
					try
					{
						blindedData = ByteHelpers.FromHex(request.BlindedOutputHex);
					}
					catch
					{
						return BadRequest("Invalid blinded output hex.");
					}
					byte[] signature = RsaKey.SignBlindedData(blindedData);

					// Check if phase changed since.
					if (round.Status != CcjRoundStatus.Running || round.Phase != CcjRoundPhase.InputRegistration)
					{
						return StatusCode(StatusCodes.Status503ServiceUnavailable, "The state of the round changed while handling the request. Try again.");
					}

					// Progress round if needed.
					if(round.CountAlices() >= round.AnonymitySet)
					{
						await round.ExecuteNextPhaseAsync(CcjRoundPhase.ConnectionConfirmation);
					}

					var resp = new InputsResponse
					{
						UniqueId = alice.UniqueId,
						BlindedOutputSignature = signature
					};
					return Ok(resp);
				}
				catch(Exception ex)
				{
					Logger.LogDebug<ChaumianCoinJoinController>(ex);
					return BadRequest(ex.Message);
				}
			}
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