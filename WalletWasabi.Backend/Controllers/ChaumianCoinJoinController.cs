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

namespace WalletWasabi.Backend.Controllers
{
	/// <summary>
	/// To interact with the Chaumian CoinJoin Coordinator.
	/// </summary>
	[Produces("application/json")]
	[Route("api/vgit log " + Helpers.Constants.BackendMajorVersion + "/btc/[controller]")]
	public class ChaumianCoinJoinController : Controller
	{
		private static RPCClient RpcClient => Global.RpcClient;

		private static Network Network => Global.Config.Network;

		private static CcjCoordinator Coordinator => Global.Coordinator;

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

		public static IEnumerable<CcjRunningRoundState> GetStatesCollection()
		{
			var response = new List<CcjRunningRoundState>();

			foreach (CcjRound round in Coordinator.GetRunningRounds())
			{
				var state = new CcjRunningRoundState
				{
					Phase = round.Phase,
					PubKey = round.Signer.Key.PubKey,
					R = round.Signer.R.PubKey,
					Denomination = round.Denomination,
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
		/// <remarks>
		/// Servers' Public Keys to blind with:
		///     TestNet:
		///         Modulus: "19473594448380717274202325076521698699373476167359253614775896809797414915031772455344343455269320444157176520539924715307970060890094127521516100754263825112231545354422893125394219335109864514907655429499954825469485252969706079992227103439161156022844535556626007277544637236136559868400854764962522288139619969507311597914908752685925185380735570791798593290356424409633800092336087046668579610273133131498947353719917407262847070395909920415822288443947309434039008038907229064999576278651443575362470457496666718250346530518268694562965606704838796709743032825816642704620776596590683042135764246115456630753521"
		///         Exponent: "65537"
		///     MainNet:
		///         Modulus: "16421152619146079007287475569112871971988560541093277613438316709041030720662622782033859387192362542996510605015506477964793447620206674394713753349543444988246276357919473682408472170521463339860947351211455351029147665615454176157348164935212551240942809518428851690991984017733153078846480521091423447691527000770982623947706172997649440619968085147635776736938871139581019988225202983052255684151711253254086264386774936200194229277914886876824852466823571396538091430866082004097086602287294474304344865162932126041736158327600847754258634325228417149098062181558798532036659383679712667027126535424484318399849"
		///         Exponent: "65537"
		/// </remarks>
		/// <response code="200">BlindedOutputSignature, UniqueId, RoundId</response>
		/// <response code="400">If request is invalid.</response>
		/// <response code="503">If the round status changed while fulfilling the request.</response>
		[HttpPost("inputs")]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(503)]
		public async Task<IActionResult> PostInputsAsync([FromBody, Required]InputsRequest request)
		{
			// Validate request.
			if (!ModelState.IsValid)
			{
				var error = ModelState.Values.SelectMany(e => e.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
				return BadRequest(!error.EndsWith("field is required.") ? error : "Invalid request.");
			}

			using (await InputsLock.LockAsync())
			{
				CcjRound round = Coordinator.GetCurrentInputRegisterableRound();

				// Do more checks.
				try
				{
					if (round.ContainsBlindedOutputScript(request.BlindedOutputScript, out _))
					{
						return BadRequest("Blinded output has already been registered.");
					}

					var inputs = new HashSet<Coin>();

					var alicesToRemove = new HashSet<Guid>();

					foreach (InputProofModel inputProof in request.Inputs)
					{
						if (inputs.Any(x => x.Outpoint == inputProof.Input))
						{
							return BadRequest("Cannot register an input twice.");
						}
						if (round.ContainsInput(inputProof.Input.ToOutPoint(), out List<Alice> tr))
						{
							alicesToRemove.UnionWith(tr.Select(x => x.UniqueId)); // Input is already registered by this alice, remove it later if all the checks are completed fine.
						}
						if (Coordinator.AnyRunningRoundContainsInput(inputProof.Input.ToOutPoint(), out List<Alice> tnr))
						{
							if (tr.Union(tnr).Count() > tr.Count())
							{
								return BadRequest("Input is already registered in another round.");
							}
						}

						OutPoint outpoint = inputProof.Input.ToOutPoint();
						var bannedElem = await Coordinator.UtxoReferee.TryGetBannedAsync(outpoint, notedToo: false);
						if (bannedElem != null)
						{
							return BadRequest($"Input is banned from participation for {(int)bannedElem.Value.bannedRemaining.TotalMinutes} minutes: {inputProof.Input.Index}:{inputProof.Input.TransactionId}.");
						}

						GetTxOutResponse getTxOutResponse = await RpcClient.GetTxOutAsync(inputProof.Input.TransactionId, (int)inputProof.Input.Index, includeMempool: true);

						// Check if inputs are unspent.
						if (getTxOutResponse is null)
						{
							return BadRequest($"Provided input is not unspent: {inputProof.Input.Index}:{inputProof.Input.TransactionId}.");
						}

						// Check if unconfirmed.
						if (getTxOutResponse.Confirmations <= 0)
						{
							// If it spends a CJ then it may be acceptable to register.
							if (!Coordinator.ContainsCoinJoin(inputProof.Input.TransactionId))
							{
								return BadRequest("Provided input is neither confirmed, nor is from an unconfirmed coinjoin.");
							}

							// Check if mempool would accept a fake transaction created with the registered inputs.
							// This will catch ascendant/descendant count and size limits for example.
							var result = await RpcClient.TestMempoolAcceptAsync(new Coin(inputProof.Input.ToOutPoint(), getTxOutResponse.TxOut));

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

						TxOut txout = getTxOutResponse.TxOut;

						var address = (BitcoinWitPubKeyAddress)txout.ScriptPubKey.GetDestinationAddress(Network);
						// Check if proofs are valid.
						bool validProof;
						try
						{
							validProof = address.VerifyMessage(request.BlindedOutputScript, inputProof.Proof);
						}
						catch (FormatException ex)
						{
							return BadRequest($"Provided proof is invalid: {ex.Message}");
						}
						if (!validProof)
						{
							await Coordinator.UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, round.RoundId, outpoint);

							return BadRequest("Provided proof is invalid.");
						}

						inputs.Add(new Coin(inputProof.Input.ToOutPoint(), txout));
					}

					// Check if inputs have enough coins.
					Money inputSum = inputs.Sum(x => x.Amount);
					Money networkFeeToPay = (inputs.Count() * round.FeePerInputs) + (2 * round.FeePerOutputs);
					Money changeAmount = inputSum - (round.Denomination + networkFeeToPay);
					if (changeAmount < Money.Zero)
					{
						return BadRequest($"Not enough inputs are provided. Fee to pay: {networkFeeToPay.ToString(false, true)} BTC. Round denomination: {round.Denomination.ToString(false, true)} BTC. Only provided: {inputSum.ToString(false, true)} BTC.");
					}

					// Make sure Alice checks work.
					var alice = new Alice(inputs, networkFeeToPay, request.ChangeOutputAddress, request.BlindedOutputScript);

					foreach (Guid aliceToRemove in alicesToRemove)
					{
						round.RemoveAlicesBy(aliceToRemove);
					}
					round.AddAlice(alice);

					BigInteger signature = round.Signer.Sign(request.BlindedOutputScript);

					// Check if phase changed since.
					if (round.Status != CcjRoundStatus.Running || round.Phase != CcjRoundPhase.InputRegistration)
					{
						return base.StatusCode(StatusCodes.Status503ServiceUnavailable, "The state of the round changed while handling the request. Try again.");
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
			if (!(returnFailureResponse is null))
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
							IEnumerable<Alice> alicesToBan = await round.RemoveAlicesIfAnInputRefusedByMempoolAsync(); // So ban only those who confirmed participation, yet spent their inputs.

							if (alicesToBan.Any())
							{
								await Coordinator.UtxoReferee.BanUtxosAsync(1, DateTimeOffset.UtcNow, forceNoted: false, round.RoundId, alicesToBan.SelectMany(x => x.Inputs).Select(y => y.Outpoint).ToArray());
							}

							int aliceCountAfterConnectionConfirmationTimeout = round.CountAlices();
							int didNotConfirmeCount = round.AnonymitySet - aliceCountAfterConnectionConfirmationTimeout;
							if (didNotConfirmeCount > 0)
							{
								round.Abort(nameof(ChaumianCoinJoinController), $"{didNotConfirmeCount} Alices did not confirem their connection.");
							}
							else
							{
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
			if (!(returnFailureResponse is null))
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
				|| request is null
				|| string.IsNullOrWhiteSpace(request.OutputAddress)
				|| request.Signature is null
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			CcjRound round = Coordinator.TryGetRound(roundHash);
			if (round is null)
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

			if (round.Signer.VerifySignature(outputAddress.ScriptPubKey.ToBytes(), request.Signature ))
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
			if (!(returnFailureResponse is null))
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
				|| signatures is null
				|| !signatures.Any()
				|| signatures.Any(x => x.Key < 0 || string.IsNullOrWhiteSpace(x.Value))
				|| !ModelState.IsValid)
			{
				return BadRequest();
			}

			(CcjRound round, Alice alice) = GetRunningRoundAndAliceOrFailureResponse(roundId, uniqueId, out IActionResult returnFailureResponse);
			if (!(returnFailureResponse is null))
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
									return BadRequest($"Input is already signed.");
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
									return BadRequest($"Invalid witness is provided. ScriptError: {error}.");
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

			if (!(guidFail is null))
			{
				returnFailureResponse = guidFail;
				return (null, null);
			}

			CcjRound round = Coordinator.TryGetRound(roundId);

			if (round is null)
			{
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
