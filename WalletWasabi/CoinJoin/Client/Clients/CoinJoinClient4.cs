using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class CoinJoinClient4 : CoinJoinClientBase
	{
		public CoinJoinClient4(WasabiSynchronizer synchronizer, Network network, KeyManager keyManager)
			: base(synchronizer, network, keyManager)
		{
		}

		protected override async Task<AliceClientBase> CreateAliceClientAsync(long roundId, RoundStateResponseBase stateParam, List<OutPoint> registrableCoins, (HdPubKey change, IEnumerable<HdPubKey> actives) outputAddresses)
		{
			RoundStateResponse4 state = null;

			var torClient = Synchronizer.WasabiClient.TorClient;
			using (var satoshiClient = new SatoshiClient(torClient.DestinationUriAction, torClient.TorSocks5EndPoint))
			{
				state = (RoundStateResponse4)await satoshiClient.GetRoundStateAsync(roundId).ConfigureAwait(false);
			}

			PubKey[] signerPubKeys = state.SignerPubKeys.ToArray();
			PublicNonceWithIndex[] numerateNonces = state.RPubKeys.ToArray();
			List<Requester> requesters = new List<Requester>();
			var blindedOutputScriptHashes = new List<BlindedOutputWithNonceIndex>();

			var registeredAddresses = new List<BitcoinAddress>();
			for (int i = 0; i < state.MixLevelCount; i++)
			{
				if (outputAddresses.actives.Count() <= i)
				{
					break;
				}

				BitcoinAddress address = outputAddresses.actives.Select(x => x.GetP2wpkhAddress(Network)).ElementAt(i);

				PubKey signerPubKey = signerPubKeys[i];
				var outputScriptHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(address.ScriptPubKey.ToBytes()));
				var requester = new Requester();
				(int n, PubKey r) = (numerateNonces[i].N, numerateNonces[i].R);
				var blindedMessage = requester.BlindMessage(outputScriptHash, r, signerPubKey);
				var blindedOutputScript = new BlindedOutputWithNonceIndex(n, blindedMessage);
				requesters.Add(requester);
				blindedOutputScriptHashes.Add(blindedOutputScript);
				registeredAddresses.Add(address);
			}

			byte[] blindedOutputScriptHashesByte = ByteHelpers.Combine(blindedOutputScriptHashes.Select(x => x.BlindedOutput.ToBytes()));
			uint256 blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedOutputScriptHashesByte));

			var inputProofs = new List<InputProofModel>();
			foreach (OutPoint coinReference in registrableCoins)
			{
				SmartCoin coin = State.GetSingleOrDefaultFromWaitingList(coinReference);
				if (coin is null)
				{
					throw new NotSupportedException("This is impossible.");
				}

				coin.Secret ??= KeyManager.GetSecrets(SaltSoup(), coin.ScriptPubKey).Single();
				var inputProof = new InputProofModel
				{
					Input = coin.OutPoint,
					Proof = coin.Secret.PrivateKey.SignCompact(blindedOutputScriptsHash)
				};
				inputProofs.Add(inputProof);
			}

			return await AliceClientBase.CreateNewAsync(roundId, registeredAddresses, signerPubKeys, requesters, Network, outputAddresses.change.GetP2wpkhAddress(Network), blindedOutputScriptHashes, inputProofs, CcjHostUriAction, TorSocks5EndPoint).ConfigureAwait(false);
		}
	}
}
