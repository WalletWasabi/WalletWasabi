﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
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

		private static BlindingRsaKey RsaKey => Coordinator.RsaKey;

		/// <summary>
		/// Satoshi gets various status information.
		/// </summary>
		/// <returns>List of CcjRunningRoundStatus (Phase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent)</returns>
		/// <response code="200">List of CcjRunningRoundStatus (Phase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent)</response>
		[HttpGet("states")]
		[ProducesResponseType(200)]
		public IActionResult GetStates()
		{
			var response = new List<CcjRunningRoundState>();

			foreach (CcjRound round in Coordinator.GetRunningRounds())
			{
				var state = new CcjRunningRoundState
				{
					Phase = round.Phase,
					Denomination = round.Denomination,
					RegisteredPeerCount = round.CountAlices(syncLock: false),
					RequiredPeerCount = round.AnonymitySet,
					MaximumInputCountPerPeer = 7, // Constant for now. If we want to do something with it later, we'll put it to the config file.
					RegistrationTimeout = (int)round.AliceRegistrationTimeout.TotalSeconds,
					FeePerInputs = round.FeePerInputs,
					FeePerOutputs = round.FeePerOutputs,
					CoordinatorFeePercent = round.CoordinatorFeePercent,
					RoundId = round.RoundId
				};

				response.Add(state);
			}

			return Ok(response);
		}

		private static AsyncLock InputsLock { get; } = new AsyncLock();

		/// <summary>
		/// Alice registers her inputs.
		/// </summary>
		/// <returns>BlindedOutputSignature, UniqueId</returns>
		/// <response code="200">BlindedOutputSignature, UniqueId, RoundId</response>
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
				|| string.IsNullOrWhiteSpace(request.BlindedOutputScriptHex)
				|| string.IsNullOrWhiteSpace(request.ChangeOutputAddress)
				|| request.Inputs == null
			    || !request.Inputs.Any()
				|| request.Inputs.Any(x => x.Input == null
					|| x.Input.Hash == null
					|| string.IsNullOrWhiteSpace(x.Proof)))
			{
				return BadRequest("Invalid request.");
			}

			if (request.Inputs.Count() > 7)
			{
				return BadRequest("Maximum 7 inputs can be registered.");
			}

			using (await InputsLock.LockAsync())
			{
				CcjRound round = Coordinator.GetCurrentInputRegisterableRound();

				// Do more checks.
				try
				{
					if (round.ContainsBlindedOutputScriptHex(request.BlindedOutputScriptHex, out _))
					{
						return BadRequest("Blinded output has already been registered.");
					}

					BitcoinAddress changeOutputAddress;
					try
					{
						changeOutputAddress = BitcoinAddress.Create(request.ChangeOutputAddress, Network);
					}
					catch (FormatException ex)
					{
						return BadRequest($"Invalid ChangeOutputAddress. Details: {ex.Message}");
					}

					var inputs = new HashSet<(OutPoint OutPoint, TxOut Output)>();

					var alicesToRemove = new HashSet<Guid>();

					foreach (InputProofModel inputProof in request.Inputs)
					{
						if (inputs.Any(x => x.OutPoint == inputProof.Input))
						{
							return BadRequest("Cannot register an input twice.");
						}
						if (round.ContainsInput(inputProof.Input, out List<Alice> tr))
						{
							alicesToRemove.UnionWith(tr.Select(x => x.UniqueId)); // Input is already registered by this alice, remove it later if all the checks are completed fine.
						}
						if (Coordinator.AnyRunningRoundContainsInput(inputProof.Input, out List<Alice> tnr))
						{
							if (tr.Union(tnr).Count() > tr.Count())
							{
								return BadRequest("Input is already registered in another round.");
							}
						}

						var bannedElem = Coordinator.UtxoReferee.BannedUtxos.SingleOrDefault(x => x.Key == inputProof.Input);
						if (bannedElem.Key != default)
						{
							int maxBan = (int)TimeSpan.FromDays(30).TotalMinutes;
							int banLeft = maxBan - (int)((DateTimeOffset.UtcNow - bannedElem.Value.timeOfBan).TotalMinutes);
							if (banLeft > 0)
							{
								return BadRequest($"Input is banned from participation for {banLeft} minutes: {inputProof.Input.N}:{inputProof.Input.Hash}.");
							}
							await Coordinator.UtxoReferee.UnbanAsync(bannedElem.Key);
						}

						GetTxOutResponse getTxOutResponse = await RpcClient.GetTxOutAsync(inputProof.Input.Hash, (int)inputProof.Input.N, includeMempool: true);

						// Check if inputs are unspent.
						if (getTxOutResponse == null)
						{
							return BadRequest("Provided input is not unspent.");
						}

						// Check if unconfirmed.
						if (getTxOutResponse.Confirmations <= 0)
						{
							// If it spends a CJ then it may be acceptable to register.
							if (!Coordinator.ContainsCoinJoin(inputProof.Input.Hash))
							{
								return BadRequest("Provided input is neither confirmed, nor is from an unconfirmed coinjoin.");
							}
							// After 24 unconfirmed cj in the mempool dont't let unconfirmed coinjoin to be registered.
							if (await Coordinator.IsUnconfirmedCoinJoinLimitReachedAsync())
							{
								return BadRequest("Provided input is from an unconfirmed coinjoin, but the maximum number of unconfirmed coinjoins is reached.");
							}
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
							validProof = address.VerifyMessage(request.BlindedOutputScriptHex, inputProof.Proof);
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
					Money inputSum = inputs.Select(x=>x.Output.Value).Sum();
					Money networkFeeToPay = (inputs.Count() * round.FeePerInputs + 2 * round.FeePerOutputs);
					Money changeAmount = inputSum - (round.Denomination + networkFeeToPay);
					if (changeAmount < Money.Zero)
					{
						return BadRequest($"Not enough inputs are provided. Fee to pay: {networkFeeToPay.ToString(false, true)} BTC. Round denomination: {round.Denomination.ToString(false, true)} BTC. Only provided: {inputSum.ToString(false, true)} BTC.");
					}

					// Make sure Alice checks work.
					var alice = new Alice(inputs, networkFeeToPay, changeOutputAddress, request.BlindedOutputScriptHex);

					foreach (Guid aliceToRemove in alicesToRemove)
					{
						round.RemoveAlicesBy(aliceToRemove);
					}
					round.AddAlice(alice);

					// All checks are good. Sign.
					byte[] blindedData;
					try
					{
						blindedData = ByteHelpers.FromHex(request.BlindedOutputScriptHex);
					}
					catch
					{
						return BadRequest("Invalid blinded output hex.");
					}

					byte[] signature = RsaKey.SignBlindedData(blindedData);

					// Check if phase changed since.
					if (round.Status != CcjRoundStatus.Running || round.Phase != CcjRoundPhase.InputRegistration)
					{
						return base.StatusCode(StatusCodes.Status503ServiceUnavailable, "The state of the round changed while handling the request. Try again.");
					}

					// Progress round if needed.
					if (round.CountAlices() >= round.AnonymitySet)
					{
						await round.RemoveAlicesIfInputsSpentAsync();

						if (round.CountAlices() >= round.AnonymitySet)
						{
							await round.ExecuteNextPhaseAsync(CcjRoundPhase.ConnectionConfirmation);
						}
					}

					var resp = new InputsResponse
					{
						UniqueId = alice.UniqueId,
						BlindedOutputSignature = signature,
						RoundId = round.RoundId
					};
					return Ok(resp);
				}
				catch (Exception ex)
				{
					Logger.LogDebug<ChaumianCoinJoinController>(ex);
					return BadRequest(ex.Message);
				}
			}
		}

		/// <summary>
		/// Alice must confirm her participation periodically in InputRegistration phase and confirm once in ConnectionConfirmation phase.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <param name="roundId">Round identifier, obtained previously.</param>
		/// <returns>RoundHash if the phase is already ConnectionConfirmation.</returns>
		/// <response code="200">RoundHash if the phase is already ConnectionConfirmation.</response>
		/// <response code="204">If the phase is InputRegistration and Alice is found.</response>
		/// <response code="400">The provided uniqueId or roundId was malformed.</response>
		/// <response code="404">If Alice or the round is not found.</response>
		/// <response code="410">Participation can be only confirmed from a Running round's InputRegistration or ConnectionConfirmation phase.</response>
		[HttpPost("confirmation")]
		[ProducesResponseType(200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(410)]
		public async Task<IActionResult> PostConfirmationAsync([FromQuery]string uniqueId, [FromQuery]long roundId)
		{
			if (roundId <= 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			CcjRoundPhase phase = round.Phase;
			switch (phase)
			{
				case CcjRoundPhase.InputRegistration:
					{
						round.StartAliceTimeout(alice.UniqueId);
						return NoContent();
					}
				case CcjRoundPhase.ConnectionConfirmation:
					{
						alice.State = AliceState.ConnectionConfirmed;

						// Progress round if needed.
						if (round.AllAlices(AliceState.ConnectionConfirmed))
						{
							IEnumerable<Alice> alicesToBan = await round.RemoveAlicesIfInputsSpentAsync(); // So ban only those who confirmed participation, yet spent their inputs.

							if (alicesToBan.Any())
							{
								await Coordinator.UtxoReferee.BanUtxosAsync(1, DateTimeOffset.Now, alicesToBan.SelectMany(x => x.Inputs).Select(y => y.OutPoint).ToArray());
							}

							int aliceCountAfterConnectionConfirmationTimeout = round.CountAlices();
							if (aliceCountAfterConnectionConfirmationTimeout < 2)
							{
								round.Fail();
							}
							else
							{
								round.UpdateAnonymitySet(aliceCountAfterConnectionConfirmationTimeout);
								// Progress to the next phase, which will be OutputRegistration
								await round.ExecuteNextPhaseAsync(CcjRoundPhase.OutputRegistration);
							}
						}

						return Ok(round.RoundHash); // Participation can be confirmed multiple times, whatever.
					}
				default:
					{
						return Gone($"Participation can be only confirmed from InputRegistration or ConnectionConfirmation phase. Current phase: {phase}.");
					}
			}
		}

		/// <summary>
		/// Alice can revoke her registration without penalty if the current phase is InputRegistration.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <param name="roundId">Round identifier, obtained previously.</param>
		/// <response code="200">Alice or the round was not found.</response>
		/// <response code="204">Alice sucessfully uncofirmed her participation.</response>
		/// <response code="400">The provided uniqueId or roundId was malformed.</response>
		/// <response code="410">Participation can be only unconfirmed from a Running round's InputRegistration phase.</response>
		[HttpPost("unconfirmation")]
		[ProducesResponseType(200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(410)]
		public IActionResult PostUnconfimation([FromQuery]string uniqueId, [FromQuery]long roundId)
		{
			if (roundId <= 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			Guid uniqueIdGuid = GetGuidOrFailureResponse(uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			CcjRound round = Coordinator.TryGetRound(roundId);
			if (round == null)
			{
				return Ok("Round not found.");
			}

			Alice alice = round.TryGetAliceBy(uniqueIdGuid);

			if (alice == null)
			{
				return Ok("Alice not found.");
			}

			if (round.Status != CcjRoundStatus.Running)
			{
				return Gone("Round is not running.");
			}

			CcjRoundPhase phase = round.Phase;
			switch (phase)
			{
				case CcjRoundPhase.InputRegistration:
					{
						round.RemoveAlicesBy(uniqueIdGuid);
						return NoContent();
					}
				default:
					{
						return Gone($"Participation can be only unconfirmed from InputRegistration phase. Current phase: {phase}.");
					}
			}
		}

		private static AsyncLock OutputLock { get; } = new AsyncLock();

		/// <summary>
		/// Bob registers his output.
		/// </summary>
		/// <param name="roundHash">Hash of the round, obtained previously.</param>
		/// <returns>RoundHash if the phase is already ConnectionConfirmation.</returns>
		/// <response code="204">Output is successfully registered.</response>
		/// <response code="400">The provided roundHash or outpurRequest was malformed.</response>
		/// <response code="409">Output registration can only be done from OutputRegistration phase.</response>
		/// <response code="410">Output registration can only be done from a Running round.</response>
		/// <response code="404">If round not found.</response>
		[HttpPost("output")]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(409)]
		[ProducesResponseType(410)]
		public async Task<IActionResult> PostOutputAsync([FromQuery]string roundHash, [FromBody]OutputRequest request)
		{
			if (string.IsNullOrWhiteSpace(roundHash)
				|| request == null
				|| string.IsNullOrWhiteSpace(request.OutputAddress)
				|| string.IsNullOrWhiteSpace(request.SignatureHex)
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			CcjRound round = Coordinator.TryGetRound(roundHash);
			if (round == null)
			{
				return NotFound("Round not found.");
			}

			if (round.Status != CcjRoundStatus.Running)
			{
				return Gone("Round is not running.");
			}

			CcjRoundPhase phase = round.Phase;
			if (phase != CcjRoundPhase.OutputRegistration)
			{
				return Conflict($"Output registration can only be done from OutputRegistration phase. Current phase: {phase}.");
			}

			BitcoinAddress outputAddress;
			try
			{
				outputAddress = BitcoinAddress.Create(request.OutputAddress, Network);
			}
			catch (FormatException ex)
			{
				return BadRequest($"Invalid OutputAddress. Details: {ex.Message}");
			}

			if (RsaKey.PubKey.Verify(ByteHelpers.FromHex(request.SignatureHex), outputAddress.ScriptPubKey.ToBytes()))
			{
				using (await OutputLock.LockAsync())
				{
					Bob bob = null;
					try
					{
						bob = new Bob(outputAddress);
						round.AddBob(bob);
					}
					catch (Exception ex)
					{
						return BadRequest($"Invalid outputAddress is provided. Details: {ex.Message}");
					}

					if (round.CountBobs() == round.AnonymitySet)
					{
						await round.ExecuteNextPhaseAsync(CcjRoundPhase.Signing);
					}
				}

				return NoContent();
			}
			return BadRequest("Invalid signature provided.");
		}

		/// <summary>
		/// Alice asks for the final CoinJoin transaction.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <param name="roundId">Round identifier, obtained previously.</param>
		/// <returns>Hx of the coinjoin transaction.</returns>
		/// <response code="200">Returns the coinjoin transaction.</response>
		/// <response code="400">The provided uniqueId or roundId was malformed.</response>
		/// <response code="404">If Alice or the round is not found.</response>
		/// <response code="409">CoinJoin can only be requested from Signing phase.</response>
		/// <response code="410">CoinJoin can only be requested from a Running round.</response>
		[HttpGet("coinjoin")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(409)]
		[ProducesResponseType(410)]
		public IActionResult GetCoinJoin([FromQuery]string uniqueId, [FromQuery]long roundId)
		{
			if (roundId <= 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			CcjRoundPhase phase = round.Phase;
			switch (phase)
			{
				case CcjRoundPhase.Signing:
					{
						return Ok(round.GetUnsignedCoinJoinHex());
					}
				default:
					{
						return Conflict($"CoinJoin can only be requested from Signing phase. Current phase: {phase}.");
					}
			}
		}

		private static AsyncLock SigningLock { get; } = new AsyncLock();

		/// <summary>
		/// Alice posts her witnesses.
		/// </summary>
		/// <param name="uniqueId">Unique identifier, obtained previously.</param>
		/// <param name="roundId">Round identifier, obtained previously.</param>
		/// <param name="signatures">Dictionary that has an int index as its key and string witness as its value.</param>
		/// <returns>Hx of the coinjoin transaction.</returns>
		/// <response code="204">CoinJoin successfully signed.</response>
		/// <response code="400">The provided uniqueId, roundId or witnesses were malformed.</response>
		/// <response code="409">Signatures can only be provided from Signing phase.</response>
		/// <response code="410">Signatures can only be provided from a Running round.</response>
		/// <response code="404">If Alice or the round is not found.</response>
		[HttpPost("signatures")]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(409)]
		[ProducesResponseType(410)]
		public async Task<IActionResult> PostSignaturesAsync([FromQuery]string uniqueId, [FromQuery]long roundId, [FromBody]IDictionary<int, string> signatures)
		{
			if (roundId <= 0
				|| signatures == null
			    || !signatures.Any()
				|| signatures.Any(x => x.Key < 0 || string.IsNullOrWhiteSpace(x.Value))
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			// Check if Alice provided signature to all her inputs.
			if (signatures.Count != alice.Inputs.Count())
			{
				return BadRequest("Alice did not provide enough witnesses.");
			}

			CcjRoundPhase phase = round.Phase;
			switch (phase)
			{
				case CcjRoundPhase.Signing:
					{
						using (await SigningLock.LockAsync())
						{
							foreach (var signaturePair in signatures)
							{
								int index = signaturePair.Key;
								WitScript witness = null;
								try
								{
									witness = new WitScript(signaturePair.Value);
								}
								catch (Exception ex)
								{
									return BadRequest($"Malformed witness is provided. Details: {ex.Message}");
								}
								int maxIndex = round.UnsignedCoinJoin.Inputs.Count - 1;
								if (maxIndex < index)
								{
									return BadRequest($"Index out of range. Maximum value: {maxIndex}. Provided value: {index}");
								}

								// Check duplicates.
								if (round.SignedCoinJoin.Inputs[index].HasWitness())
								{
									return BadRequest($"Input is already signed.");
								}

								// Verify witness.
								var cjCopy = new Transaction(round.UnsignedCoinJoin.ToHex());
								cjCopy.Inputs[index].WitScript = witness;
								TxOut output = alice.Inputs.Single(x => x.OutPoint == cjCopy.Inputs[index].PrevOut).Output;
								if (!Script.VerifyScript(output.ScriptPubKey, cjCopy, index, output.Value, ScriptVerify.Standard, SigHash.All))
								{
									return BadRequest($"Invalid witness is provided.");
								}

								// Finally add it to our CJ.
								round.SignedCoinJoin.Inputs[index].WitScript = witness;
							}

							alice.State = AliceState.SignedCoinJoin;

							await round.BroadcastCoinJoinIfFullySignedAsync();
						}

						return NoContent();
					}
				default:
					{
						return Conflict($"CoinJoin can only be requested from Signing phase. Current phase: {phase}.");
					}
			}
		}

		private Guid GetGuidOrFailureResponse(string uniqueId, out IActionResult returnFailureResponse)
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

			return aliceGuid;
		}

		private (CcjRound round, Alice alice) GetRunningRoundAndAliceOrFailureResponse(long roundId, string uniqueId, out IActionResult returnFailureResponse)
		{
			returnFailureResponse = null;

			Guid uniqueIdGuid = GetGuidOrFailureResponse(uniqueId, out IActionResult guidFail);

			if (guidFail != null)
			{
				returnFailureResponse = guidFail;
				return (null, null);
			}

			CcjRound round = Coordinator.TryGetRound(roundId);

			if (round == null)
			{
				returnFailureResponse = NotFound("Round not found.");
				return (null, null);
			}

			Alice alice = round.TryGetAliceBy(uniqueIdGuid);
			if (alice == null)
			{
				returnFailureResponse = NotFound("Alice not found.");
				return (round, null);
			}

			if (round.Status != CcjRoundStatus.Running)
			{
				returnFailureResponse = Gone("Round is not running.");
			}

			return (round, alice);
		}

		/// <summary>
		/// 409
		/// </summary>
		private ContentResult Conflict(string content) => new ContentResult() { StatusCode = (int)HttpStatusCode.Conflict, ContentType = "application/json; charset=utf-8", Content = $"\"{content}\"" };

		/// <summary>
		/// 410
		/// </summary>
		private ContentResult Gone(string content) => new ContentResult() { StatusCode = (int)HttpStatusCode.Gone, ContentType = "application/json; charset=utf-8", Content = $"\"{content}\"" };
	}
}
