using HiddenWallet.ChaumianCoinJoin;
using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.Crypto;
using HiddenWallet.FullSpv;
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
		public string BaseAddress { get; private set; }

		public SafeAccount From { get; private set; }
		public SafeAccount To { get; private set; }

		public BlindingRsaPubKey PubKey { get; private set; }

		private volatile bool _tumblingInProcess;
		public volatile TumblerPhase Phase;

		public CoinJoinService(WalletJob walletJob)
		{
			_tumblingInProcess = false;
			TumblerConnection = null;
			WalletJob = walletJob ?? throw new ArgumentNullException(nameof(walletJob));
		}

		public void SetConnection(string address, BlindingRsaPubKey pubKey, HttpMessageHandler handler, bool disposeHandler = false)
		{
			PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
			BaseAddress = address.EndsWith('/') ? address : address + "/";
			TumblerClient = new ChaumianTumblerClient(BaseAddress, handler, disposeHandler);
		}

		#region Notifications

		public async Task SubscribePhaseChangeAsync()
		{
			try
			{
				TumblerConnection = new HubConnectionBuilder()
						.WithUrl(BaseAddress + "ChaumianTumbler")
						.Build();

				TumblerConnection.On<string>("PhaseChange", async data =>
				{
					var jObject = JObject.Parse(data);
					var phaseString = jObject.Value<string>("NewPhase");

					Phase = TumblerPhaseHelpers.GetTumblerPhase(phaseString);
					if (Phase != TumblerPhase.InputRegistration) // input registration is executed from tumbling function
					{
						await ExecutePhaseAsync(Phase, CancellationToken.None);
					}

					Debug.WriteLine($"New Phase: {phaseString}");
				});

				await TumblerConnection.StartAsync();

				TumblerConnection.Closed += TumblerConnection_ClosedAsync;
			}
			catch (Exception ex)
			{
				await DisposeAsync();
				Debug.WriteLine(ex);
			}
		}

		private async Task TumblerConnection_ClosedAsync(Exception arg)
		{
			await DisposeAsync();
		}

		#endregion

		#region Tumbling
		
		/// <summary>
		/// Participates one tumbling round
		/// </summary>
		public async Task TumbleAsync(SafeAccount from, SafeAccount to, CancellationToken cancel)
		{
			try
			{
				From = from ?? throw new ArgumentNullException(nameof(from));
				To = to ?? throw new ArgumentNullException(nameof(to));

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

				if(TumblerConnection == null)
				{
					await SubscribePhaseChangeAsync();

					if (TumblerConnection == null)
					{
						throw new HttpRequestException("Tumbler is offline");
					}
				}

				StatusResponse status = await TumblerClient.GetStatusAsync(cancel);

				Phase = TumblerPhaseHelpers.GetTumblerPhase(status.Phase);
				
				// wait for input registration
				while(Phase != TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}

				_tumblingInProcess = true;
				await ExecutePhaseAsync(Phase, cancel);

				// wait while input registration
				while (Phase == TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}

				// wait for input registration again, this is the end of the round (beginning of the next round)
				while (Phase != TumblerPhase.InputRegistration)
				{
					await Task.Delay(100, cancel);
				}
				_tumblingInProcess = false;
			}
			catch(OperationCanceledException)
			{
				return;
			}
		}

		private async Task ExecutePhaseAsync(TumblerPhase phase, CancellationToken cancel)
		{
			if (_tumblingInProcess)
			{
				try
				{
					if (phase == TumblerPhase.InputRegistration)
					{
						StatusResponse status = await TumblerClient.GetStatusAsync(cancel);
						var getBalanceResult = await WalletJob.GetBalanceAsync(From);
						var balance = getBalanceResult.Available.Confirmed + getBalanceResult.Available.Unconfirmed;
						var denomination = Money.Parse(status.Denomination);
						var feePerInputs = Money.Parse(status.FeePerInputs);
						var feePerOutputs = Money.Parse(status.FeePerOutputs);
						Money needed = denomination + (feePerInputs * getBalanceResult.UnspentCoins.Count) + (feePerOutputs * 2);
						if (balance < needed) // TODO: only native segwit (p2wpkh) inputs are accepted by the tumbler (later wrapped segwit (p2sh-p2wpkh) will be accepted too)
						{
							throw new InvalidOperationException("Not enough coins");
						}
						IEnumerable<Script> unusedOutputs = await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, To, HdPathType.NonHardened);
						Script output = unusedOutputs.RandomElement(); // TODO: this is sub-optimal, it'd be better to not which had been already registered and not reregister it
						var blindingResult = PubKey.Blind(output.ToBytes());
						string blindedOutput = HexHelpers.ToString(blindingResult.BlindedData);

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

							var input = coin.Outpoint.ToHex();
							BitcoinExtKey privateExtKey = WalletJob.Safe.FindPrivateKey(coin.ScriptPubKey.GetDestinationAddress(WalletJob.CurrentNetwork), (await WalletJob.GetTrackerAsync()).TrackedScriptPubKeys.Count, From);
							var proof = privateExtKey.PrivateKey.SignMessage(blindedOutput);
							inputs.Add(new InputProofModel { Input = input, Proof = proof });

							sumAmounts += coin.Amount;
							if (sumAmounts >= needed)
							{
								break;
							}
						}

						IEnumerable<Script> changeOutputCandidates = await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, From, HdPathType.Change);
						Script changeOutputScript = changeOutputCandidates.RandomElement(); // TODO: this is sub-optimal, it'd be better to not which had been already registered and not reregister it
						BitcoinAddress changeOutput = changeOutputScript.GetDestinationAddress(WalletJob.CurrentNetwork);

						var request = new InputsRequest
						{
							ChangeOutput = changeOutput.ToString(),
							BlindedOutput = blindedOutput,
							Inputs = inputs.ToArray()
						};
						var response = await TumblerClient.PostInputsAsync(request, cancel);
						
						// unblind the signature
						var unblindedSignature = PubKey.UnblindSignature(HexHelpers.GetBytes(response.SignedBlindedOutput), blindingResult.BlindingFactor);
						// verify the original data is signed
						if (!PubKey.Verify(unblindedSignature, output.ToBytes()))
						{
							throw new HttpRequestException("Tumbler did not sign the blinded output properly");
						}
					}
					else if (phase == TumblerPhase.ConnectionConfirmation)
					{
						throw new NotImplementedException();
					}
					else if (phase == TumblerPhase.OutputRegistration)
					{
						throw new NotImplementedException();
					}
					else if (phase == TumblerPhase.Signing)
					{
						throw new NotImplementedException();
					}
					else throw new NotSupportedException("This should never happen");
				}
				catch
				{
					// if an exception happened don't tumbler anymore in this round
					_tumblingInProcess = false;
					throw;
				}
			}
		}

		#endregion

		#region Disposing

		public async Task DisposeAsync()
		{
			try
			{
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
