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
	public class CoinJoinClient : CoinJoinClientBase
	{
		public CoinJoinClient(WasabiSynchronizer synchronizer, Network network, KeyManager keyManager)
			: base(synchronizer, network, keyManager)
		{
		}

		protected override async Task<AliceClientBase> CreateAliceClientAsync(long roundId, RoundStateResponseBase stateParam, List<OutPoint> registrableCoins, (HdPubKey change, IEnumerable<HdPubKey> actives) outputAddresses)
		{
			var state = stateParam as RoundStateResponse;
			SchnorrPubKey[] schnorrPubKeys = state.SchnorrPubKeys.ToArray();
			List<Requester> requesters = new List<Requester>();
			var blindedOutputScriptHashes = new List<uint256>();

			var registeredAddresses = new List<BitcoinAddress>();
			for (int i = 0; i < state.MixLevelCount; i++)
			{
				if (outputAddresses.actives.Count() <= i)
				{
					break;
				}

				BitcoinAddress address = outputAddresses.actives.Select(x => x.GetP2wpkhAddress(Network)).ElementAt(i);

				SchnorrPubKey schnorrPubKey = schnorrPubKeys[i];
				var outputScriptHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(address.ScriptPubKey.ToBytes()));
				var requester = new Requester();
				uint256 blindedOutputScriptHash = requester.BlindMessage(outputScriptHash, schnorrPubKey);
				requesters.Add(requester);
				blindedOutputScriptHashes.Add(blindedOutputScriptHash);
				registeredAddresses.Add(address);
			}

			byte[] blindedOutputScriptHashesByte = ByteHelpers.Combine(blindedOutputScriptHashes.Select(x => x.ToBytes()));
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

			return await AliceClientBase.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, Network, outputAddresses.change.GetP2wpkhAddress(Network), blindedOutputScriptHashes, inputProofs, CcjHostUriAction, TorSocks5EndPoint).ConfigureAwait(false);
		}
	}
}
