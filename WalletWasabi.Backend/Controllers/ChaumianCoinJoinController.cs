using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Requests;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To interact with the Chaumian CoinJoin Coordinator.
	/// </summary>
	[Produces("application/json")]
	[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
	public class ChaumianCoinJoinController : Controller
	{
		public Global Global { get; }
		private RPCClient RpcClient => Global.RpcClient;
		private Network Network => Global.Config.Network;
		private CcjCoordinator Coordinator => Global.Coordinator;

		public ChaumianCoinJoinController(Global global)
		{
			Global = global;
		}

		/// <summary>
		/// Satoshi gets various status information.
		/// </summary>
		/// <returns>List of CcjRunningRoundStatus (Phase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, RoundId, SuccessfulRoundCount)</returns>
		/// <response code="200">List of CcjRunningRoundStatus (Phase, Denomination, RegisteredPeerCount, RequiredPeerCount, MaximumInputCountPerPeer, FeePerInputs, FeePerOutputs, CoordinatorFeePercent, RoundId, SuccessfulRoundCount)</response>
		[HttpGet("states")]
		[ProducesResponseType(200)]
		public IActionResult GetStates()
		{
			IEnumerable<CcjRunningRoundState> response = GetStatesCollection();

			return Ok(response);
		}

		internal IEnumerable<CcjRunningRoundState> GetStatesCollection()
		{
			var response = new List<CcjRunningRoundState>();

			foreach (CcjRound round in Coordinator.GetRunningRounds())
			{
				var state = new CcjRunningRoundState
				{
					Phase = round.Phase,
					SchnorrPubKeys = round.MixingLevels.SchnorrPubKeys,
					Denomination = round.MixingLevels.GetBaseDenomination(),
					InputRegistrationTimesout = round.InputRegistrationTimesout,
					RegisteredPeerCount = round.CountAlices(syncLock: false),
					RequiredPeerCount = round.AnonymitySet,
					MaximumInputCountPerPeer = 7, // Constant for now. If we want to do something with it later, we'll put it to the config file.
					RegistrationTimeout = (int)round.AliceRegistrationTimeout.TotalSeconds,
					FeePerInputs = round.FeePerInputs,
					FeePerOutputs = round.FeePerOutputs,
					CoordinatorFeePercent = round.CoordinatorFeePercent,
					RoundId = round.RoundId,
					SuccessfulRoundCount = Coordinator.GetCoinJoinCount() // This is round independent, it is only here because of backward compatibility.
				};

				response.Add(state);
			}

			return response;
		}

		private static AsyncLock InputsLock { get; } = new AsyncLock();

		/// <summary>
		/// Alice registers her inputs.
		/// </summary>
		/// <returns>BlindedOutputSignature, UniqueId</returns>
		/// <response code="200">BlindedOutputSignature, UniqueId, RoundId</response>
		/// <response code="400">If request is invalid.</response>
		/// <response code="404">Round not found or it is not in InputRegistration anymore.</response>
		[HttpPost("inputs")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		public async Task<IActionResult> PostInputsAsync([FromBody, Required]InputsRequest request)
		{
			// Validate request.
			if (request.RoundId < 0 || !ModelState.IsValid)
			{
				return BadRequest("Invalid request.");
			}

			if (request.Inputs.Count() > 7)
			{
				return BadRequest("Maximum 7 inputs can be registered.");
			}

			using (await InputsLock.LockAsync())
			{
				CcjRound round = Coordinator.TryGetRound(request.RoundId);

				if (round is null || round.Phase != CcjRoundPhase.InputRegistration)
				{
					return NotFound($"No such running round in {nameof(CcjRoundPhase.InputRegistration)}. Try another round.");
				}

				// Do more checks.
				try
				{
					uint256[] blindedOutputs = request.BlindedOutputScripts.ToArray();
					int blindedOutputCount = blindedOutputs.Length;
					int maxBlindedOutputCount = round.MixingLevels.Count();
					if (blindedOutputCount > maxBlindedOutputCount)
					{
						return BadRequest($"Too many blinded output was provided: {blindedOutputCount}, maximum: {maxBlindedOutputCount}.");
					}

					if (blindedOutputs.Distinct().Count() < blindedOutputs.Length)
					{
						return BadRequest("Duplicate blinded output found.");
					}

					if (round.ContainsAnyBlindedOutputScript(blindedOutputs))
					{
						return BadRequest("Blinded output has already been registered.");
					}

					if (request.ChangeOutputAddress.Network != Network)
					{
						// RegTest and TestNet address formats are sometimes the same.
						if (Network == Network.Main)
						{
							return BadRequest($"Invalid {nameof(request.ChangeOutputAddress)} Network.");
						}
					}

					var uniqueInputs = new HashSet<TxoRef>();
					foreach (InputProofModel inputProof in request.Inputs)
					{
						if (uniqueInputs.Contains(inputProof.Input))
						{
							return BadRequest("Cannot register an input twice.");
						}
						uniqueInputs.Add(inputProof.Input);
					}

					var alicesToRemove = new HashSet<Guid>();
					var getTxOutResponses = new List<(InputProofModel inputModel, Task<GetTxOutResponse> getTxOutTask)>();

					var batch = RpcClient.PrepareBatch();

					foreach (InputProofModel inputProof in request.Inputs)
					{
						if (round.ContainsInput(inputProof.Input.ToOutPoint(), out List<Alice> tr))
						{
							alicesToRemove.UnionWith(tr.Select(x => x.UniqueId)); // Input is already registered by this alice, remove it later if all the checks are completed fine.
						}
						if (Coordinator.AnyRunningRoundContainsInput(inputProof.Input.ToOutPoint(), out List<Alice> tnr))
						{
							if (tr.Union(tnr).Count() > tr.Count)
							{
								return BadRequest("Input is already registered in another round.");
							}
						}

						OutPoint outpoint = inputProof.Input.ToOutPoint();
						var bannedElem = await Coordinator.UtxoReferee.TryGetBannedAsync(outpoint, notedToo: false);
						if (bannedElem != null)
						{
							return BadRequest($"Input is banned from participation for {(int)bannedElem.BannedRemaining.TotalMinutes} minutes: {inputProof.Input.Index}:{inputProof.Input.TransactionId}.");
						}

						var txOutResponseTask = batch.GetTxOutAsync(inputProof.Input.TransactionId, (int)inputProof.Input.Index, includeMempool: true);
						getTxOutResponses.Add((inputProof, txOutResponseTask));
					}

					// Perform all RPC request at once
					var waiting = Task.WhenAll(getTxOutResponses.Select(x => x.getTxOutTask));
					await batch.SendBatchAsync();
					await waiting;

					byte[] blindedOutputScriptHashesByte = ByteHelpers.Combine(blindedOutputs.Select(x => x.ToBytes()));
					uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blindedOutputScriptHashesByte));

					var inputs = new HashSet<Coin>();

					foreach (var responses in getTxOutResponses)
					{
						var (inputProof, getTxOutResponseTask) = responses;
						var getTxOutResponse = await getTxOutResponseTask;

						// Check if inputs are unspent.
						if (getTxOutResponse is null)
						{
							return BadRequest($"Provided input is not unspent: {inputProof.Input.Index}:{inputProof.Input.TransactionId}.");
						}

						// Check if unconfirmed.
						if (getTxOutResponse.Confirmations <= 0)
						{
							// If it spends a CJ then it may be acceptable to register.
							if (!await Coordinator.ContainsCoinJoinAsync(inputProof.Input.TransactionId))
							{
								return BadRequest("Provided input is neither confirmed, nor is from an unconfirmed coinjoin.");
							}

							// Check if mempool would accept a fake transaction created with the registered inputs.
							// This will catch ascendant/descendant count and size limits for example.
							var result = await RpcClient.TestMempoolAcceptAsync(new[] { new Coin(inputProof.Input.ToOutPoint(), getTxOutResponse.TxOut) });
							if (!result.accept)
							{
								return BadRequest($"Provided input is from an unconfirmed coinjoin, but a limit is reached: {result.rejectReason}");
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

						TxOut txOut = getTxOutResponse.TxOut;

						var address = (BitcoinWitPubKeyAddress)txOut.ScriptPubKey.GetDestinationAddress(Network);
						// Check if proofs are valid.
						if (!address.VerifyMessage(blindedOutputScriptsHash, inputProof.Proof))
						{
							return BadRequest("Provided proof is invalid.");
						}

						inputs.Add(new Coin(inputProof.Input.ToOutPoint(), txOut));
					}

					var acceptedBlindedOutputScripts = new List<uint256>();

					// Calculate expected networkfee to pay after base denomination.
					int inputCount = inputs.Count;
					Money networkFeeToPayAfterBaseDenomination = (inputCount * round.FeePerInputs) + (2 * round.FeePerOutputs);

					// Check if inputs have enough coins.
					Money inputSum = inputs.Sum(x => x.Amount);
					Money changeAmount = (inputSum - (round.MixingLevels.GetBaseDenomination() + networkFeeToPayAfterBaseDenomination));
					if (changeAmount < Money.Zero)
					{
						return BadRequest($"Not enough inputs are provided. Fee to pay: {networkFeeToPayAfterBaseDenomination.ToString(false, true)} BTC. Round denomination: {round.MixingLevels.GetBaseDenomination().ToString(false, true)} BTC. Only provided: {inputSum.ToString(false, true)} BTC.");
					}
					acceptedBlindedOutputScripts.Add(blindedOutputs.First());

					Money networkFeeToPay = networkFeeToPayAfterBaseDenomination;
					// Make sure we sign the proper number of additional blinded outputs.
					var moneySoFar = Money.Zero;
					for (int i = 1; i < blindedOutputCount; i++)
					{
						if (!round.MixingLevels.TryGetDenomination(i, out Money denomination))
						{
							break;
						}

						Money coordinatorFee = denomination.Percentage(round.CoordinatorFeePercent * round.AnonymitySet); // It should be the number of bobs, but we must make sure they'd have money to pay all.
						changeAmount -= (denomination + round.FeePerOutputs + coordinatorFee);
						networkFeeToPay += round.FeePerOutputs;

						if (changeAmount < Money.Zero)
						{
							break;
						}

						acceptedBlindedOutputScripts.Add(blindedOutputs[i]);
					}

					// Make sure Alice checks work.
					var alice = new Alice(inputs, networkFeeToPayAfterBaseDenomination, request.ChangeOutputAddress, acceptedBlindedOutputScripts);

					foreach (Guid aliceToRemove in alicesToRemove)
					{
						round.RemoveAlicesBy(aliceToRemove);
					}
					round.AddAlice(alice);

					// All checks are good. Sign.
					var blindSignatures = new List<uint256>();
					for (int i = 0; i < acceptedBlindedOutputScripts.Count; i++)
					{
						var blindedOutput = acceptedBlindedOutputScripts[i];
						var signer = round.MixingLevels.GetLevel(i).Signer;
						uint256 blindSignature = signer.Sign(blindedOutput);
						blindSignatures.Add(blindSignature);
					}
					alice.BlindedOutputSignatures = blindSignatures.ToArray();

					// Check if phase changed since.
					if (round.Status != CcjRoundStatus.Running || round.Phase != CcjRoundPhase.InputRegistration)
					{
						return StatusCode(StatusCodes.Status503ServiceUnavailable, "The state of the round changed while handling the request. Try again.");
					}

					// Progress round if needed.
					if (round.CountAlices() >= round.AnonymitySet)
					{
						await round.RemoveAlicesIfAnInputRefusedByMempoolAsync();

						if (round.CountAlices() >= round.AnonymitySet)
						{
							await round.ExecuteNextPhaseAsync(CcjRoundPhase.ConnectionConfirmation);
						}
					}

					var resp = new InputsResponse
					{
						UniqueId = alice.UniqueId,
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
		/// <returns>Current phase and blinded output sinatures if Alice is found.</returns>
		/// <response code="200">Current phase and blinded output sinatures if Alice is found.</response>
		/// <response code="400">The provided uniqueId or roundId was malformed.</response>
		/// <response code="404">If Alice or the round is not found.</response>
		/// <response code="410">Participation can be only confirmed from a Running round's InputRegistration or ConnectionConfirmation phase.</response>
		[HttpPost("confirmation")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(410)]
		public async Task<IActionResult> PostConfirmationAsync([FromQuery, Required]string uniqueId, [FromQuery, Required]long roundId)
		{
			if (roundId < 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, CcjRoundPhase.ConnectionConfirmation, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			CcjRoundPhase phase = round.Phase;

			// Start building the response.
			var resp = new ConnConfResp
			{
				CurrentPhase = phase
			};

			switch (phase)
			{
				case CcjRoundPhase.InputRegistration:
					{
						round.StartAliceTimeout(alice.UniqueId);
						break;
					}
				case CcjRoundPhase.ConnectionConfirmation:
					{
						alice.State = AliceState.ConnectionConfirmed;

						int takeBlindCount = round.EstimateBestMixingLevel(alice);

						alice.BlindedOutputScripts = alice.BlindedOutputScripts.Take(takeBlindCount).ToArray();
						alice.BlindedOutputSignatures = alice.BlindedOutputSignatures.Take(takeBlindCount).ToArray();
						resp.BlindedOutputSignatures = alice.BlindedOutputSignatures; // Do not give back more mixing levels than we'll use.

						// Progress round if needed.
						if (round.AllAlices(AliceState.ConnectionConfirmed))
						{
							await round.ProgressToOutputRegistrationOrFailAsync();
						}

						break;
					}
				default:
					{
						TryLogLateRequest(roundId, CcjRoundPhase.ConnectionConfirmation);
						return Gone($"Participation can be only confirmed from {nameof(CcjRoundPhase.InputRegistration)} or " +
							$"{nameof(CcjRoundPhase.ConnectionConfirmation)} phase. Current phase: {phase}.");
					}
			}

			return Ok(resp);
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
		public IActionResult PostUnconfimation([FromQuery, Required]string uniqueId, [FromQuery, Required]long roundId)
		{
			if (roundId < 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			Guid uniqueIdGuid = GetGuidOrFailureResponse(uniqueId, out IActionResult returnFailureResponse);
			if (returnFailureResponse != null)
			{
				return returnFailureResponse;
			}

			CcjRound round = Coordinator.TryGetRound(roundId);
			if (round is null)
			{
				return Ok("Round not found.");
			}

			Alice alice = round.TryGetAliceBy(uniqueIdGuid);

			if (alice is null)
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
						return Gone($"Participation can be only unconfirmed from {nameof(CcjRoundPhase.InputRegistration)} phase. Current phase: {phase}.");
					}
			}
		}

		private static AsyncLock OutputLock { get; } = new AsyncLock();

		/// <summary>
		/// Bob registers his output.
		/// </summary>
		/// <param name="roundId">RoundId.</param>
		/// <response code="204">Output is successfully registered.</response>
		/// <response code="400">The provided roundId or outputRequest was malformed.</response>
		/// <response code="409">Output registration can only be done from OutputRegistration phase.</response>
		/// <response code="410">Output registration can only be done from a Running round.</response>
		/// <response code="404">If round not found.</response>
		[HttpPost("output")]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(409)]
		[ProducesResponseType(410)]
		public async Task<IActionResult> PostOutputAsync([FromQuery, Required]long roundId, [FromBody, Required]OutputRequest request)
		{
			if (roundId < 0
				|| request.Level < 0
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			CcjRound round = Coordinator.TryGetRound(roundId);
			if (round is null)
			{
				TryLogLateRequest(roundId, CcjRoundPhase.OutputRegistration);
				return NotFound("Round not found.");
			}

			if (round.Status != CcjRoundStatus.Running)
			{
				TryLogLateRequest(roundId, CcjRoundPhase.OutputRegistration);
				return Gone("Round is not running.");
			}

			CcjRoundPhase phase = round.Phase;
			if (phase != CcjRoundPhase.OutputRegistration)
			{
				TryLogLateRequest(roundId, CcjRoundPhase.OutputRegistration);
				return Conflict($"Output registration can only be done from {nameof(CcjRoundPhase.OutputRegistration)} phase. Current phase: {phase}.");
			}

			if (request.OutputAddress.Network != Network)
			{
				// RegTest and TestNet address formats are sometimes the same.
				if (Network == Network.Main)
				{
					return BadRequest($"Invalid {nameof(request.OutputAddress)} Network.");
				}
			}

			if (request.OutputAddress == Constants.GetCoordinatorAddress(Network))
			{
				Logger.LogWarning<ChaumianCoinJoinController>($"Bob is registering the coordinator's address. Address: {request.OutputAddress}, Level: {request.Level}, Signature: {request.UnblindedSignature}.");
			}

			if (request.Level > round.MixingLevels.GetMaxLevel())
			{
				return BadRequest($"Invalid mixing level is provided. Provided: {request.Level}. Maximum: {round.MixingLevels.GetMaxLevel()}.");
			}

			if (round.ContainsRegisteredUnblindedSignature(request.UnblindedSignature))
			{
				return NoContent();
			}

			MixingLevel mixinglevel = round.MixingLevels.GetLevel(request.Level);
			Signer signer = mixinglevel.Signer;

			if (signer.VerifyUnblindedSignature(request.UnblindedSignature, request.OutputAddress.ScriptPubKey.ToBytes()))
			{
				using (await OutputLock.LockAsync())
				{
					Bob bob = null;
					try
					{
						bob = new Bob(request.OutputAddress, mixinglevel);
						round.AddBob(bob);
						round.AddRegisteredUnblindedSignature(request.UnblindedSignature);
					}
					catch (Exception ex)
					{
						return BadRequest($"Invalid {nameof(request.OutputAddress)} is provided. Details: {ex.Message}");
					}

					int bobCount = round.CountBobs();
					int blindSigCount = round.CountBlindSignatures();
					if (bobCount == blindSigCount) // If there'll be more bobs, then round failed. Someone may broke the crypto.
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
		public IActionResult GetCoinJoin([FromQuery, Required]string uniqueId, [FromQuery, Required]long roundId)
		{
			if (roundId < 0 || !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, _) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, CcjRoundPhase.Signing, out IActionResult returnFailureResponse);
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
						TryLogLateRequest(roundId, CcjRoundPhase.Signing);
						return Conflict($"CoinJoin can only be requested from {CcjRoundPhase.Signing} phase. Current phase: {phase}.");
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
		public async Task<IActionResult> PostSignaturesAsync([FromQuery, Required]string uniqueId, [FromQuery, Required]long roundId, [FromBody, Required]IDictionary<int, string> signatures)
		{
			if (roundId < 0
				|| !signatures.Any()
				|| signatures.Any(x => x.Key < 0 || string.IsNullOrWhiteSpace(x.Value))
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, CcjRoundPhase.Signing, out IActionResult returnFailureResponse);
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
								if (round.SignedCoinJoin.Inputs[index].HasWitScript())
								{
									return BadRequest("Input is already signed.");
								}

								// Verify witness.
								// 1. Copy UnsignedCoinJoin.
								Transaction cjCopy = Transaction.Parse(round.UnsignedCoinJoin.ToHex(), Network);
								// 2. Sign the copy.
								cjCopy.Inputs[index].WitScript = witness;
								// 3. Convert the current input to IndexedTxIn.
								IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();
								// 4. Find the corresponding registered input.
								Coin registeredCoin = alice.Inputs.Single(x => x.Outpoint == cjCopy.Inputs[index].PrevOut);
								// 5. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
								if (!currentIndexedInput.VerifyScript(registeredCoin, out ScriptError error))
								{
									return BadRequest($"Invalid witness is provided. {nameof(ScriptError)}: {error}.");
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
						TryLogLateRequest(roundId, CcjRoundPhase.Signing);
						return Conflict($"CoinJoin can only be requested from {CcjRoundPhase.Signing} phase. Current phase: {phase}.");
					}
			}
		}

		private Guid GetGuidOrFailureResponse(string uniqueId, out IActionResult returnFailureResponse)
		{
			returnFailureResponse = null;
			if (string.IsNullOrWhiteSpace(uniqueId) || !ModelState.IsValid)
			{
				returnFailureResponse = BadRequest($"Invalid {nameof(uniqueId)} provided.");
			}

			Guid aliceGuid = Guid.Empty;
			try
			{
				aliceGuid = Guid.Parse(uniqueId);
			}
			catch (Exception ex)
			{
				Logger.LogDebug<ChaumianCoinJoinController>(ex);
				returnFailureResponse = BadRequest($"Invalid {nameof(uniqueId)} provided.");
			}
			if (aliceGuid == Guid.Empty) // Probably not possible
			{
				Logger.LogDebug<ChaumianCoinJoinController>($"Empty {nameof(uniqueId)} GID provided in {nameof(GetCoinJoin)} function.");
				returnFailureResponse = BadRequest($"Invalid {nameof(uniqueId)} provided.");
			}

			return aliceGuid;
		}

		private (CcjRound round, Alice alice) GetRunningRoundAndAliceOrFailureResponse(long roundId, string uniqueId, CcjRoundPhase desiredPhase, out IActionResult returnFailureResponse)
		{
			returnFailureResponse = null;

			Guid uniqueIdGuid = GetGuidOrFailureResponse(uniqueId, out IActionResult guidFail);

			if (guidFail != null)
			{
				returnFailureResponse = guidFail;
				return (null, null);
			}

			CcjRound round = Coordinator.TryGetRound(roundId);

			if (round is null)
			{
				TryLogLateRequest(roundId, desiredPhase);
				returnFailureResponse = NotFound("Round not found.");
				return (null, null);
			}

			Alice alice = round.TryGetAliceBy(uniqueIdGuid);
			if (alice is null)
			{
				returnFailureResponse = NotFound("Alice not found.");
				return (round, null);
			}

			if (round.Status != CcjRoundStatus.Running)
			{
				TryLogLateRequest(roundId, desiredPhase);
				returnFailureResponse = Gone("Round is not running.");
			}

			return (round, alice);
		}

		private static void TryLogLateRequest(long roundId, CcjRoundPhase desiredPhase)
		{
			try
			{
				DateTimeOffset ended = CcjRound.PhaseTimeoutLog.TryGet((roundId, desiredPhase));
				if (ended != default)
				{
					Logger.LogInfo<ChaumianCoinJoinController>($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss} {desiredPhase} {(int)(DateTimeOffset.UtcNow - ended).TotalSeconds} seconds late.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug<ChaumianCoinJoinController>(ex);
			}
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
