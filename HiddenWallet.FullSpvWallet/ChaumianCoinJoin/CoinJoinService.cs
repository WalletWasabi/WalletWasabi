using ConcurrentCollections;
using DotNetTor;
using HiddenWallet.ChaumianCoinJoin;
using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.Crypto;
using HiddenWallet.FullSpv;
using HiddenWallet.Helpers;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;
using Microsoft.AspNetCore.SignalR.Client;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HiddenWallet.FullSpv.WalletJob;

namespace HiddenWallet.FullSpvWallet.ChaumianCoinJoin
{
	public class CoinJoinService
	{
		public HubConnection TumblerConnection { get; private set; }
		public WalletJob WalletJob { get; private set; }
		public ChaumianTumblerClient TumblerClient { get; private set; }
		public string NotificationBaseAddress { get; private set; }

		public SafeAccount From { get; private set; }

		public BlindingRsaPubKey PubKey { get; private set; }

		public volatile bool TumblingInProcess;
		public volatile TumblerPhase Phase;
		public volatile Exception TumblingException;
		public volatile bool CompletedLastPhase;
		public volatile Transaction CoinJoin;

		public Money Denomination => Money.Parse(StatusResponse?.Denomination ?? "0");
		public StatusResponse StatusResponse { get; set; }
		public int NumberOfPeers { get; private set; }

		public string UniqueAliceId { get; private set; }
		public BitcoinWitPubKeyAddress ActiveOutput { get; private set; }
		public BitcoinAddress ChangeOutput { get; private set; }
		public Money ChangeOutputExpectedValue { get; private set; }
		public ConcurrentHashSet<BitcoinExtKey> SigningKeys { get; private set; }
		public ConcurrentHashSet<Coin> Inputs { get; private set; }
		public string UnblindedSignature { get; private set; }
		public string RoundHash { get; private set; }

		public CoinJoinService(WalletJob walletJob)
		{
			CompletedLastPhase = true;
			TumblingInProcess = false;
			UniqueAliceId = null;
			TumblerConnection = null;
			CoinJoin = null;
			TumblingException = null;
			WalletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
		}

		public void SetConnection(string address, string notificationAddress, BlindingRsaPubKey pubKey, HttpMessageHandler handler, bool disposeHandler = false)
		{
			PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
			if (address == null) throw new ArgumentNullException(address);
			if (notificationAddress == null) throw new ArgumentNullException(notificationAddress);
			NotificationBaseAddress = notificationAddress.Trim().EndsWith('/') ? notificationAddress.Trim() : notificationAddress.Trim() + "/";
			var correctedAddress = address.Trim().EndsWith('/') ? address.Trim() : address.Trim() + "/";
			TumblerClient = new ChaumianTumblerClient(correctedAddress, handler, disposeHandler);
		}

		#region Notifications

		public async Task SubscribePhaseChangeAsync()
		{
			try
			{
				TumblerConnection = new HubConnectionBuilder()
						.WithUrl(NotificationBaseAddress + "ChaumianTumbler")
						.Build();

				TumblerConnection.On<string>("PhaseChange", async data =>
				{
					var jObject = JObject.Parse(data);
					var phaseString = jObject.Value<string>("NewPhase");

					Debug.WriteLine($"New Phase: {phaseString}");
					var phase = TumblerPhaseHelpers.GetTumblerPhase(phaseString);
					Phase = phase;
					if (phase != TumblerPhase.InputRegistration) // input registration is executed from tumbling function
					{
						try
						{
							await ExecutePhaseAsync(phase);
						}
						catch
						{
							// handle inside
						}
					}
					Debug.WriteLine($"Phase completed");
				});

				TumblerConnection.On<string>("PeerRegistered", data =>
				{
					var jObject = JObject.Parse(data);
					var numberOfPeers = jObject.Value<int>("NewRegistration");
					NumberOfPeers = numberOfPeers;

					Debug.WriteLine($"Number of Tumbler peers: {numberOfPeers}");
				});

				await TumblerConnection.StartAsync();

				TumblerConnection.Closed += TumblerConnection_ClosedAsync;
			}
			catch (Exception ex)
			{
				await TumblerConnection?.DisposeAsync();
				TumblerConnection = null;

				Debug.WriteLine(ex);
			}
		}

		private async Task TumblerConnection_ClosedAsync(Exception arg)
		{
			await TumblerConnection?.DisposeAsync();
			TumblerConnection = null;
		}

		#endregion

		#region Tumbling
		
		/// <summary>
		/// Participates one tumbling round
		/// </summary>
		public async Task<uint256> TumbleAsync(SafeAccount from, BitcoinWitPubKeyAddress to, CancellationToken cancel)
		{
			try
			{
				CoinJoin = null;
				TumblingException = null;

				From = from ?? throw new ArgumentNullException(nameof(from));
				ActiveOutput = to ?? throw new ArgumentNullException(nameof(to));

				// if blocks are not synced yet throw
				var headerHeightResult = await WalletJob.TryGetHeaderHeightAsync();
				if (!headerHeightResult.Success)
				{
					throw new InvalidOperationException("Cannot get header height");
				}
				Height headerHeight = headerHeightResult.Height;
				Height blockHeight = await WalletJob.GetBestHeightAsync();

				if (headerHeight.Value - 2 > blockHeight) // tolerate being maximum two blocks behind
				{
					throw new InvalidOperationException("Blocks are not synced");
				}

				if (TumblerConnection == null)
				{
					await SubscribePhaseChangeAsync();

					if (TumblerConnection == null)
					{
						throw new HttpRequestException("Tumbler is offline");
					}
				}

				StatusResponse status;
				try
				{
					status = await TumblerClient.GetStatusAsync(cancel);
				}
				catch (TorException)
				{
					// ToDo: fix it in DotNetTor, this happens when the tumbler goes offline, then comes back up, the already established connection cannot be reused
					status = await TumblerClient.GetStatusAsync(cancel); 
				}
				StatusResponse = status;

				Phase = TumblerPhaseHelpers.GetTumblerPhase(status.Phase);
				
				// wait for input registration
				while(Phase != TumblerPhase.InputRegistration)
				{
					if (TumblerConnection == null) throw new HttpRequestException("Server went offline");
					status = null;
					await Task.Delay(100, cancel);
				}

				TumblingInProcess = true;
				await ExecutePhaseAsync(TumblerPhase.InputRegistration, status, cancel);
				
				// wait while tumbling is in process
				while (TumblingInProcess)
				{
					if (TumblerConnection == null) throw new HttpRequestException("Server went offline");
					await Task.Delay(100, cancel);
				}

				// if tumbling 
				if (TumblingException != null)
				{
					throw TumblingException;
				}
				else
				{
					return CoinJoin?.GetHash();
				}
			}
			catch(OperationCanceledException)
			{
				return null;
			}
		}

		private async Task ExecutePhaseAsync(TumblerPhase phase, StatusResponse statusJustAcquired = null, CancellationToken cancel = default)
		{
			while (CompletedLastPhase == false)
			{
				await Task.Delay(100, cancel);
			}
			if (TumblingInProcess)
			{
				try
				{
					CompletedLastPhase = false;
					if (phase == TumblerPhase.InputRegistration)
					{
						StatusResponse status = statusJustAcquired ?? await TumblerClient.GetStatusAsync(cancel);
						StatusResponse = status;
						// TODO: only native segwit (p2wpkh) inputs are accepted by the tumbler (later wrapped segwit (p2sh-p2wpkh) will be accepted too)
						var getBalanceResult = await WalletJob.GetBalanceAsync(From);
						var balance = getBalanceResult.Available.Confirmed + getBalanceResult.Available.Unconfirmed;
						var feePerInputs = Money.Parse(status.FeePerInputs);
						var feePerOutputs = Money.Parse(status.FeePerOutputs);

						var blindingResult = PubKey.Blind(Encoding.UTF8.GetBytes(ActiveOutput.ToString()));
						string blindedOutput = HexHelpers.ToString(blindingResult.BlindedData);

						SigningKeys = new ConcurrentHashSet<BitcoinExtKey>();
						Inputs = new ConcurrentHashSet<Coin>();
						var inputs = new List<InputProofModel>();
						var i = 0;
						Money sumAmounts = Money.Zero;
						foreach (Coin coin in getBalanceResult.UnspentCoins
							.OrderByDescending(x => x.Value) // look at confirmed ones first
							.OrderByDescending(x => x.Key.Amount) // look at the biggest amounts first, TODO: this is sub-optimal (CCJ unconfirmed change should be the same category as confirmed and not CCJ change unconfirmed should not be here)
							.Select(x => x.Key))
						{
							i++;
							if (i > status.MaximumInputsPerAlices)
							{
								throw new InvalidOperationException($"Maximum {status.MaximumInputsPerAlices} can be registered");
							}

							Money needed = Denomination + (feePerInputs * i) + (feePerOutputs * 2);

							Inputs.Add(coin);
							var input = coin.Outpoint.ToHex();
							BitcoinExtKey privateExtKey = WalletJob.Safe.FindPrivateKey(coin.ScriptPubKey.GetDestinationAddress(WalletJob.CurrentNetwork), (await WalletJob.GetTrackerAsync()).TrackedScriptPubKeys.Count, From);
							SigningKeys.Add(privateExtKey);
							var proof = privateExtKey.PrivateKey.SignMessage(blindedOutput);
							inputs.Add(new InputProofModel { Input = input, Proof = proof });

							sumAmounts += coin.Amount;
							if (sumAmounts >= needed)
							{
								ChangeOutputExpectedValue = sumAmounts - needed;
								break;
							}

							if (i == getBalanceResult.UnspentCoins.Count)
							{
								throw new InvalidOperationException("Not enough coins to participate in mix");
							}
						}

						IEnumerable<Script> changeOutputCandidates = await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, From, HdPathType.Change);
						Script changeOutputScript = changeOutputCandidates.RandomElement(); // TODO: this is sub-optimal, it'd be better to not which had been already registered and not reregister it
						ChangeOutput = changeOutputScript.GetDestinationAddress(WalletJob.CurrentNetwork);

						var request = new InputsRequest
						{
							ChangeOutput = ChangeOutput.ToString(),
							BlindedOutput = blindedOutput,
							Inputs = inputs.ToArray()
						};
						var response = await TumblerClient.PostInputsAsync(request, cancel);
						UniqueAliceId = response.UniqueId;

						// unblind the signature
						var unblindedSignature = PubKey.UnblindSignature(HexHelpers.GetBytes(response.SignedBlindedOutput), blindingResult.BlindingFactor);
						// verify the original data is signed
						if (!PubKey.Verify(unblindedSignature, Encoding.UTF8.GetBytes(ActiveOutput.ToString())))
						{
							throw new HttpRequestException("Tumbler did not sign the blinded output properly");
						}
						UnblindedSignature = HexHelpers.ToString(unblindedSignature);
					}
					else if (phase == TumblerPhase.ConnectionConfirmation)
					{
						var request = new ConnectionConfirmationRequest
						{
							UniqueId = UniqueAliceId
						};
						var resp = await TumblerClient.PostConnectionConfirmationAsync(request, cancel);
						RoundHash = resp.RoundHash;
					}
					else if (phase == TumblerPhase.OutputRegistration)
					{
						Debug.WriteLine($"Changing Tor circuit");
						await WalletJob.ControlPortClient.ChangeCircuitAsync();
						await Task.Delay(100);

						var request = new OutputRequest
						{
							Output = ActiveOutput.ToString(),
							Signature = UnblindedSignature,
							RoundHash = RoundHash
						};
						await TumblerClient.PostOutputAsync(request, cancel);
					}
					else if (phase == TumblerPhase.Signing)
					{
						Debug.WriteLine($"Changing Tor circuit");
						await WalletJob.ControlPortClient.ChangeCircuitAsync();
						await Task.Delay(100);

						var request = new CoinJoinRequest
						{
							UniqueId = UniqueAliceId
						};
						CoinJoin = new Transaction((await TumblerClient.PostCoinJoinAsync(request, cancel)).Transaction);

						if (!(CoinJoin.Outputs.Any(x => x.ScriptPubKey == ActiveOutput.ScriptPubKey && x.Value >= Denomination)))
						{
							throw new InvalidOperationException("Tumbler did not add enough value to the active output");
						}
						if (!(CoinJoin.Outputs.Any(x => x.ScriptPubKey == ChangeOutput.ScriptPubKey && x.Value >= ChangeOutputExpectedValue)))
						{
							throw new InvalidOperationException("Tumbler did not add enough value to the change output");
						}
						if (RoundHash != NBitcoinHelpers.HashOutpoints(CoinJoin.Inputs.Select(x=>x.PrevOut)))
						{
							throw new InvalidOperationException("Tumbler provided invalid roundHash");
						}

						new TransactionBuilder()
							.AddKeys(SigningKeys.ToArray())
							.AddCoins(Inputs)
							.SignTransactionInPlace(CoinJoin, SigHash.All);

						var witnesses = new HashSet<(string Witness, int Index)>();
						for (int i = 0; i < CoinJoin.Inputs.Count; i++)
						{
							if (CoinJoin.Inputs[i].WitScript != null)
							{
								witnesses.Add((CoinJoin.Inputs[i].WitScript.ToString(), i));
							}
						}

						var sigRequest = new SignatureRequest
						{
							UniqueId = UniqueAliceId,
							Signatures = witnesses
						};
						await TumblerClient.PostSignatureAsync(sigRequest, cancel);
						TumblingInProcess = false;
						UniqueAliceId = null;
					}
					else throw new NotSupportedException("This should never happen");
				}
				catch (Exception ex)
				{
					// if an exception happened don't tumbler anymore in this round
					TumblingInProcess = false;
					UniqueAliceId = null;
					TumblingException = ex;
					throw;
				}
				finally
				{
					CompletedLastPhase = true;
				}
			}
		}

		/// <summary>
		/// throws if couldn't cancel
		/// </summary>
		public async Task CancelMixAsync(CancellationToken cancel)
		{
			if (UniqueAliceId != null)
			{
				var request = new DisconnectionRequest { UniqueId = UniqueAliceId };
				await TumblerClient.PostDisconnectionAsync(request, cancel);
			}
		}

		#endregion

		#region Disposing

		public async Task DisposeAsync()
		{
			try
			{

				try
				{
					await CancelMixAsync(CancellationToken.None);
				}
				catch
				{

				}

				if (TumblerConnection != null)
				{
					try
					{
						TumblerConnection.Closed -= TumblerConnection_ClosedAsync;
					}
					catch (Exception)
					{

					}
					await TumblerConnection?.DisposeAsync();
					TumblerConnection = null;
				}

				TumblerClient?.Dispose();
			}
			catch (Exception)
			{

			}
		}

		#endregion
	}
}
