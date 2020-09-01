using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.StrobeProtocol;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public delegate IEnumerable<Scalar> NoncesSequence();

	// High level API for transcripts of compound Sigma protocol style proofs
	// implements synthetic nonces and Fiat-Shamir challenges.
	public sealed class Transcript
	{
		private const int KeySizeInBytes = 32;

		private Strobe128 _strobe;

		private static readonly byte[] StatementTag = Encoding.UTF8.GetBytes("statement");
		private static readonly byte[] ChallengeTag = Encoding.UTF8.GetBytes("challenge");
		private static readonly byte[] NonceTag = Encoding.UTF8.GetBytes("nonce-commitment");
		private static readonly byte[] DomainSeparatorTag = Encoding.UTF8.GetBytes("domain-separator");


		/// <summary>
		/// Initialize a new transcript with the supplied <param>label</param>, which
		/// is used as a domain separator.
		/// </summary>
		/// <remarks>
		/// This function should be called by a proof library's API consumer 
		/// (i.e., the application using the proof library), and
		/// **not by the proof implementation**.  See the [Passing
		/// Transcripts](https://merlin.cool/use/passing.html) section of
		/// the Merlin website for more details on why.
		/// </remarks>
		public Transcript(byte[] label)
			: this(new Strobe128("WabiSabi_v1.0"))
		{
			AddMessage(DomainSeparatorTag, label);
		}

		// Private constructor used for cloning.
		private Transcript(Strobe128 strobe)
		{
			_strobe = strobe;
		}

		public Transcript MakeCopy() =>
			new Transcript(_strobe.MakeCopy());

		// Generate synthetic nonce using current state combined with additional randomness.
		public NoncesSequence CreateSyntheticNocesProvider(IEnumerable<Scalar> secrets, WasabiRandom random)
		{
			// To integrate prior inputs for deterministic component of nonce
			// generation, first clone the state at the current point in the
			// transcript, which should already have the statement tag and public
			// inputs committed.
			var forked = _strobe.MakeCopy();

			// add secret inputs as key material
			foreach (var secret in secrets)
			{
				forked.Key(secret.ToBytes(), false);
			}

			// Add additional randomness
			forked.Key(random.GetBytes(KeySizeInBytes), false);

			IEnumerable<Scalar> NoncesGenerator()
			{
				while (true)
				{
					yield return new Scalar(forked.Prf(KeySizeInBytes, false));
				}
			}

			// Generate a new scalar for each secret using this updated state as a seed.
			return NoncesGenerator;
		}
		
		public void CommitPublicNonces(IEnumerable<GroupElement> nonces)
		{
			CryptoGuard.NotInfinity(nameof(nonces), nonces);
			AddMessages(NonceTag, nonces.Select(x => x.ToBytes()));
		}

		// Generate Fiat Shamir challenges
		public Scalar GenerateChallenge()
		{
			_strobe.AddAssociatedMetaData(ChallengeTag, false);
			return new Scalar(_strobe.Prf(KeySizeInBytes, false));
		}

		private void AddMessage(byte[] label, byte[] message)
		{
			_strobe.AddAssociatedMetaData(label, false);
			_strobe.AddAssociatedMetaData(BitConverter.GetBytes(message.Length), true);
			_strobe.AddAssociatedData(message, false);
		}

		private void AddMessages(byte[] label, IEnumerable<byte[]> messages)
		{
			_strobe.AddAssociatedMetaData(label, false);
			_strobe.AddAssociatedMetaData(BitConverter.GetBytes(messages.Count()), true);
			foreach (var message in messages.Select((m, i) => (Index: i, Payload: m) ))
			{
				AddMessage(BitConverter.GetBytes(message.Index), message.Payload);
			}
		}
	}
}
