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
	// High level API for transcripts of compound Sigma protocol style proofs
	// implements synthetic nonces and Fiat-Shamir challenges

	// TODO introduce delegates for the same phases as IFSProver for individual
	// sub-proofs of conjunctions?
	//
	// it's probably overkill but this could ensure each individual sigma
	// protocol's transcript can must proceed in the right order and are
	// phase-locked (e.g. no statement commitments after nonces were generated)
	public sealed class Transcript
	{
		private Strobe128 _strobe;

		private const int KeySizeInBytes = 32;
		private static readonly byte[] StatementTag = Encoding.UTF8.GetBytes("statement");
		private static readonly byte[] ChallengeTag = Encoding.UTF8.GetBytes("challenge");
		private static readonly byte[] NonceTag = Encoding.UTF8.GetBytes("nonce_commitment");

		// public constructor always adds domain separator
		public Transcript()
		{
			_strobe = new Strobe128("WabiSabi_v1.0");
		}

		// private constructor used for cloning
		private Transcript(Strobe128 strobe)
		{
			_strobe = strobe;
		}

		public Transcript MakeCopy() =>
			new Transcript(_strobe.MakeCopy());


		// generate synthetic nonce using current state combined with additional randomness
		public IEnumerable<Scalar> GenerateSecretNonces(IEnumerable<Scalar> secrets, WasabiRandom random)
		{
			// to integrate prior inputs for deterministic component of nonce
			// generation, first clone the state at the current point in the
			// transcript, which should already have the statement tag and public
			// inputs committed.
			var forked = _strobe.MakeCopy();

			// add secret inputs as key material
			foreach (var secret in secrets)
			{
				forked.Key(secret.ToBytes(), false);
			}

			// add additional randomness
			forked.Key(random.GetBytes(KeySizeInBytes), false);

			// FIXME for the general case we need publicPoints.Count() * Witness.Length
			// secret nonces per statement.
			// this method should return a delegate here so that the following lines
			// can be used repeatedly, or given the number of public inputs it could
			// just return IEnumerable<IEnumerable<Scalar>> which is probably uglier.

			// generate a new scalar for each secret using this updated state as a seed
			return Enumerable
				.Range(0, secrets.Count())
				.Select( _ => forked.Prf(KeySizeInBytes, false))
				.Select( rnd => new Scalar(rnd) );
		}

		public void CommitPublicNonces(IEnumerable<GroupElement> nonces)
		{
			// FIXME loop Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			AddMessages(NonceTag, nonces.Select(x => x.ToBytes()));
		}

		// generate Fiat Shamir challenges
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
