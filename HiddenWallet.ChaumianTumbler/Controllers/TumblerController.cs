using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HiddenWallet.SharedApi.Models;
using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.ChaumianCoinJoin;
using Org.BouncyCastle.Utilities.Encoders;
using NBitcoin;
using NBitcoin.RPC;
using HiddenWallet.ChaumianTumbler.Store;
using HiddenWallet.ChaumianTumbler.Clients;
using Nito.AsyncEx;
using HiddenWallet.ChaumianTumbler.Referee;

namespace HiddenWallet.ChaumianTumbler.Controllers
{
	[Route("api/v1/[controller]")]
	public class TumblerController : Controller
    {
		[HttpGet]
		public string Test()
		{
			return "test";
		}
		
		[Route("status")]
		[HttpGet]
		public IActionResult Status()
		{
			try
			{
				Money denomination = Global.StateMachine.Denomination;
				string denominationSting = denomination.ToString(fplus: false, trimExcessZero: true);
				return new JsonResult(new StatusResponse
				{
					Phase = Global.StateMachine.Phase.ToString(),
					Denomination = denominationSting,
					AnonymitySet = Global.StateMachine.AnonymitySet,
					TimeSpentInInputRegistrationInSeconds = (int)Global.StateMachine.TimeSpentInInputRegistration.TotalSeconds,
					MaximumInputsPerAlices = (int)Global.Config.MaximumInputsPerAlices,
					FeePerInputs = Global.StateMachine.FeePerInputs.ToString(fplus: false, trimExcessZero: true),
					FeePerOutputs = Global.StateMachine.FeePerOutputs.ToString(fplus: false, trimExcessZero: true),
					Version = "1" // client should check and if the version is newer then client should update its software
				});
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		private readonly AsyncLock InputRegistrationLock = new AsyncLock();
		[Route("inputs")]
		[HttpPost]
		public async Task<IActionResult> InputsAsync([FromBody]InputsRequest request)
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.InputRegistration || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				// Check not nulls
				if (request.BlindedOutput == null) return new BadRequestResult();
				if (request.ChangeOutput == null) return new BadRequestResult();
				if (request.Inputs == null || request.Inputs.Count() == 0) return new BadRequestResult();

				// Check format (parse everyting))
				byte[] blindedOutput = HexHelpers.GetBytes(request.BlindedOutput);
				Network network = Global.Config.Network;
				var changeOutput = new BitcoinWitPubKeyAddress(request.ChangeOutput, expectedNetwork: network);
				if (request.Inputs.Count() > Global.Config.MaximumInputsPerAlices) throw new NotSupportedException("Too many inputs provided");
				var inputs = new HashSet<(TxOut Output, OutPoint OutPoint)>();

				using (await InputRegistrationLock.LockAsync())
				{
					foreach (InputProofModel input in request.Inputs)
					{
						var op = new OutPoint();
						op.FromHex(input.Input);
						if (inputs.Any(x => x.OutPoint.Hash == op.Hash && x.OutPoint.N == op.N))
						{
							throw new ArgumentException("Attempting to register an input twice is not permitted");
						}
						if (Global.StateMachine.Alices.SelectMany(x => x.Inputs).Any(x => x.OutPoint.Hash == op.Hash && x.OutPoint.N == op.N))
						{
							throw new ArgumentException("Input is already registered by another Alice");
						}

						BannedUtxo banned = Global.UtxoReferee.Utxos.FirstOrDefault(x => x.Utxo.Hash == op.Hash && x.Utxo.N == op.N);
						if (banned != default(BannedUtxo))
						{
							var maxBan = (int)TimeSpan.FromDays(30).TotalMinutes;
							int banLeft = maxBan - (int)((DateTimeOffset.UtcNow - banned.TimeOfBan).TotalMinutes);
							throw new ArgumentException($"Input is banned for {banLeft} minutes");
						}

						var txOutResponse = await Global.RpcClient.SendCommandAsync(RPCOperations.gettxout, op.Hash.ToString(), op.N, true);
						// Check if inputs are unspent
						if (txOutResponse.Result == null)
						{
							throw new ArgumentException("Provided input is not unspent");
						}
						// Check if inputs are unconfirmed, if so check if they are part of previous CoinJoin
						if (txOutResponse.Result.Value<int>("confirmations") <= 0)
						{
							if (!Global.CoinJoinStore.Transactions
								.Any(x => x.State == CoinJoinTransactionState.Succeeded && x.Transaction.GetHash() == op.Hash))
							{
								throw new ArgumentException("Provided input is not confirmed, nor spends a previous CJ transaction");
							}
						}
						// Check coinbase > 100
						if (txOutResponse.Result.Value<int>("confirmations") < 100)
						{
							if (txOutResponse.Result.Value<bool>("coinbase"))
							{
								throw new ArgumentException("Provided input is unspendable");
							}
						}
						// Check if inputs are native segwit
						if (txOutResponse.Result["scriptPubKey"].Value<string>("type") != "witness_v0_keyhash")
						{
							throw new ArgumentException("Provided input is not witness_v0_keyhash");
						}
						var value = txOutResponse.Result.Value<decimal>("value");
						var scriptPubKey = new Script(txOutResponse.Result["scriptPubKey"].Value<string>("asm"));
						var address = (BitcoinWitPubKeyAddress)scriptPubKey.GetDestinationAddress(network);
						// Check if proofs are valid
						if (!address.VerifyMessage(request.BlindedOutput, input.Proof))
						{
							throw new ArgumentException("Provided proof is invalid");
						}
						var txout = new TxOut(new Money(value, MoneyUnit.BTC), scriptPubKey);
						inputs.Add((txout, op));
					}

					// Check if inputs have enough coins
					Money amount = Money.Zero;
					foreach (Money val in inputs.Select(x => x.Output.Value))
					{
						amount += val;
					}
					Money feeToPay = (inputs.Count() * Global.StateMachine.FeePerInputs + 2 * Global.StateMachine.FeePerOutputs);
					Money changeAmount = amount - (Global.StateMachine.Denomination + feeToPay);
					if (changeAmount < Money.Zero)
					{
						throw new ArgumentException("Total provided inputs must be > denomination + fee");
					}

					byte[] signature = Global.RsaKey.SignBlindedData(blindedOutput);
					Guid uniqueId = Guid.NewGuid();

					var alice = new Alice
					{
						UniqueId = uniqueId,
						ChangeOutput = changeOutput,
						ChangeAmount = changeAmount,
						State = AliceState.InputsRegistered
					};
					foreach (var input in inputs)
					{
						alice.Inputs.Add(input);
					}
					Global.StateMachine.Alices.Add(alice);

					if (Global.StateMachine.Alices.Count >= Global.StateMachine.AnonymitySet)
					{
						Global.StateMachine.UpdatePhase(TumblerPhase.ConnectionConfirmation);
					}

					return new ObjectResult(new InputsResponse()
					{
						UniqueId = uniqueId.ToString(),
						SignedBlindedOutput = HexHelpers.ToString(signature)
					});
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("input-registration-status")]
		[HttpGet]
		public IActionResult InputRegistrationStatus()
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.InputRegistration || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				return new ObjectResult(new InputRegistrationStatusResponse()
				{
					ElapsedSeconds = (int)Global.StateMachine.InputRegistrationStopwatch.Elapsed.TotalSeconds,
					RequiredPeerCount = Global.StateMachine.AnonymitySet,
					RegisteredPeerCount = Global.StateMachine.Alices.Count
				});
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("connection-confirmation")]
		[HttpPost]
		public IActionResult ConnectionConfirmation([FromBody]ConnectionConfirmationRequest request)
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.ConnectionConfirmation || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				if (request.UniqueId == null) return new BadRequestResult();
				Alice alice = Global.StateMachine.FindAlice(request.UniqueId, throwException: true);

				if (alice.State == AliceState.ConnectionConfirmed)
				{
					throw new InvalidOperationException("Connection is already confirmed");
				}

				alice.State = AliceState.ConnectionConfirmed;
								
				try
				{
					return new ObjectResult(new SuccessResponse());
				}
				finally
				{
					if (Global.StateMachine.Alices.All(x => x.State == AliceState.ConnectionConfirmed))
					{
						Global.StateMachine.UpdatePhase(TumblerPhase.OutputRegistration);
					}
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("output")]
		[HttpPost]
		public IActionResult Output([FromBody]OutputRequest request)
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.OutputRegistration || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				if (request.Output == null) return new BadRequestResult();
				if (request.Signature == null) return new BadRequestResult();

				var output = new BitcoinWitPubKeyAddress(request.Output, expectedNetwork: Global.Config.Network);

				if(Global.RsaKey.PubKey.Verify(HexHelpers.GetBytes(request.Signature), HexHelpers.GetBytes(request.Output)))
				{
					try
					{
						Global.StateMachine.Bobs.Add(new Bob { Output = output });
						return new ObjectResult(new SuccessResponse());
					}
					finally
					{
						if (Global.StateMachine.Alices.Count == Global.StateMachine.Bobs.Count)
						{
							Global.StateMachine.UpdatePhase(TumblerPhase.Signing);
						}
					}
				}
				else
				{
					throw new ArgumentException("Bad output");
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		[Route("coinjoin")]
		[HttpGet]
		public IActionResult CoinJoin(CoinJoinRequest request)
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.Signing || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				if (request.UniqueId == null) return new BadRequestResult();
				Alice alice = Global.StateMachine.FindAlice(request.UniqueId, throwException: true);

				if (alice.State == AliceState.AskedForCoinJoin)
				{
					throw new InvalidOperationException("CoinJoin has been already asked for");
				}

				alice.State = AliceState.AskedForCoinJoin;

				return new ObjectResult(new CoinJoinResponse
				{
					Transaction = Global.StateMachine.CoinJoin.ToHex()
				});
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}

		private readonly AsyncLock SignatureProvidedAsyncLock = new AsyncLock();
		[Route("signature")]
		[HttpPost]
		public async Task<IActionResult> SignatureAsync([FromBody]SignatureRequest request)
		{
			try
			{
				if (Global.StateMachine.Phase != TumblerPhase.Signing || !Global.StateMachine.AcceptRequest)
				{
					return new ObjectResult(new FailureResponse { Message = "Wrong phase", Details = "" });
				}

				if (request.UniqueId == null) return new BadRequestResult();
				if (request.Signatures == null) return new BadRequestResult();
				if (request.Signatures.Count() == 0) return new BadRequestResult();
				Alice alice = Global.StateMachine.FindAlice(request.UniqueId, throwException: true);

				using (await SignatureProvidedAsyncLock.LockAsync())
				{
					foreach (var signatureModel in request.Signatures)
					{
						var witness = new WitScript(signatureModel.Witness);
						if (Global.StateMachine.CoinJoin.Inputs.Count <= signatureModel.Index)
						{
							// round fails, ban alice
							await Global.UtxoReferee.BanAliceAsync(alice);
							Global.StateMachine.FallBackRound = true;
							Global.StateMachine.UpdatePhase(TumblerPhase.InputRegistration);
						}
						if(Global.StateMachine.CoinJoin.Inputs[signatureModel.Index].WitScript != default)
						{
							// round fails, ban alice
							await Global.UtxoReferee.BanAliceAsync(alice);
							Global.StateMachine.FallBackRound = true;
							Global.StateMachine.UpdatePhase(TumblerPhase.InputRegistration);
						}
						Global.StateMachine.CoinJoin.Inputs[signatureModel.Index].WitScript = witness;
						var output = alice.Inputs.Single(x => x.OutPoint == Global.StateMachine.CoinJoin.Inputs[signatureModel.Index].PrevOut).Output;
						if(!Script.VerifyScript(output.ScriptPubKey, Global.StateMachine.CoinJoin, signatureModel.Index, output.Value, ScriptVerify.Standard, SigHash.All))
						{
							// round fails, ban alice
							await Global.UtxoReferee.BanAliceAsync(alice);
							Global.StateMachine.FallBackRound = true;
							Global.StateMachine.UpdatePhase(TumblerPhase.InputRegistration);
						}
					}

					// check if fully signed
					if(Global.StateMachine.FullySignedCoinJoin)
					{
						var failed = true;
						try
						{							
							var res = await Global.RpcClient.SendCommandAsync(RPCOperations.sendrawtransaction, Global.StateMachine.CoinJoin.ToHex(), true);
							var state = CoinJoinTransactionState.Failed;
							if (Global.StateMachine.CoinJoin.GetHash().ToString() == res.ToString())
							{
								failed = false;
								state = CoinJoinTransactionState.Succeeded;
							}
							Global.CoinJoinStore.Transactions.Add(new CoinJoinTransaction
							{
								Transaction = Global.StateMachine.CoinJoin,
								DateTime = DateTimeOffset.UtcNow,
								State = state
							});
							await Global.CoinJoinStore.ToFileAsync(Global.CoinJoinStorePath);
						}
						finally
						{
							if (failed)
							{
								Global.StateMachine.FallBackRound = true;
							}
							else Global.StateMachine.FallBackRound = false;
							Global.StateMachine.UpdatePhase(TumblerPhase.InputRegistration);
						}
					}

					return new ObjectResult(new SuccessResponse());
				}
			}
			catch (Exception ex)
			{
				return new ObjectResult(new FailureResponse { Message = ex.Message, Details = ex.ToString() });
			}
		}
	}
}
