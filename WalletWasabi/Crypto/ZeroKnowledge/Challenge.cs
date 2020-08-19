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
		public static Scalar Build(GroupElement nonce, Statement statement)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);

			var publicPoint = statement.PublicPoint;
			if (publicPoint == nonce)
			{
				throw new InvalidOperationException($"{nameof(publicPoint)} and {nameof(nonce)} should not be equal.");
			}

			return HashToScalar(new[] { publicPoint, nonce }.Concat(statement.Generators));
		}

		/// <summary>
		/// Fiat Shamir heuristic.
		/// </summary>
		private static Scalar HashToScalar(IEnumerable<GroupElement> transcript)
		{
			var transcriptBytes = transcript.Select(x => x.ToBytes());
			// Make sure the length of the data is also committed to: len(data) || data
			// https://github.com/zkSNACKs/WalletWasabi/pull/4151#discussion_r470334048
			var concatenation = transcriptBytes
				.SelectMany(x => ByteHelpers.Combine(BitConverter.GetBytes(x.Length), x))
				.ToArray();

			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var hash = sha256.ComputeHash(concatenation);
			var challenge = new Scalar(hash);
			return challenge;
		}
	}
}
