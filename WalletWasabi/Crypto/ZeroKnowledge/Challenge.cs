using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class Challenge
	{
		public static Scalar Build(GroupElement publicPoint, GroupElement nonce, IEnumerable<GroupElement> generators)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			foreach (var generator in generators)
			{
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			}

			if (publicPoint == nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(nonce)} should not be equal.");
			}

			return HashToScalar(new[] { publicPoint, nonce }.Concat(generators));
		}

		public static Scalar Build(GroupElement publicPoint, GroupElement nonce, params GroupElement[] generators)
			=> Build(publicPoint, nonce, generators as IEnumerable<GroupElement>);

		private static Scalar HashToScalar(IEnumerable<GroupElement> transcript)
		{
			var concatenation = ByteHelpers.Combine(transcript.Select(x => x.ToBytes()));
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);
			return challenge;
		}
	}
}
