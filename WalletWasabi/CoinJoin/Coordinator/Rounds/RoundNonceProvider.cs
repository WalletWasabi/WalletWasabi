using NBitcoin;
using System.Collections.Generic;
using System.Security;
using System.Threading;
using WalletWasabi.CoinJoin.Common.Models;

namespace WalletWasabi.CoinJoin.Coordinator.Rounds;

public class RoundNonceProvider
{
	private ExtKey _nonceGenerator;
	private int _lastNonceIndex;
	private HashSet<int> _alreadyUsedNonceIndexes;
	private int _mixingLevels;

	public RoundNonceProvider(int mixingLevels)
	{
		_nonceGenerator = new ExtKey();
		_lastNonceIndex = -1;
		_mixingLevels = mixingLevels;
		_alreadyUsedNonceIndexes = new HashSet<int>();
	}

	public PublicNonceWithIndex GetNextNonce()
	{
		var n = Interlocked.Increment(ref _lastNonceIndex);
		var extKey = _nonceGenerator.Derive(n, hardened: true);
		return new PublicNonceWithIndex(n, extKey.GetPublicKey());
	}

	public PublicNonceWithIndex[] GetNextNoncesForMixingLevels()
	{
		var nonces = new PublicNonceWithIndex[_mixingLevels];
		for (var i = 0; i < _mixingLevels; i++)
		{
			nonces[i] = GetNextNonce();
		}
		return nonces;
	}

	public Key GetNonceKeyForIndex(int n)
	{
		if (!_alreadyUsedNonceIndexes.Add(n))
		{
			throw new SecurityException($"Nonce {n} was already used.");
		}
		var extKey = _nonceGenerator.Derive(n, hardened: true);
		return extKey.PrivateKey;
	}
}
