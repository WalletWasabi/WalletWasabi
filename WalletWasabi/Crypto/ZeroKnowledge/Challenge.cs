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

		public static Scalar Build(IEnumerable<Statement> statements, IEnumerable<GroupElement> nonces)
		{
			return HashToScalar(statements.Select(x => x.PublicPoint).Concat(statements.SelectMany(x => x.Generators)).Concat(nonces));
		}
	}
}
