using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	// High level API for transcripts of compound Sigma protocol style proofs
	// implements synthetic nonces and Fiat-Shamir challenges
	//
	// in the future there should be multiple Transcripts sharing a single state,
	// each enforcing a state machine for every sub-proof (statement identifier,
	// public inputs, nonce generation, challenge, responses, synchronized by some
	// parent object)
	public class Transcript
	{
		private TranscriptState _state; // keeps a running hash of the transcript

		public const string DomainSeparator = "WabiSabi v0.0";

		// public constructor always adds domain separator
		public Transcript()
		{
			_state = new TranscriptState(ByteHelpers.CombineHash(Encoding.UTF8.GetBytes(DomainSeparator)));
		}

		// private constructor used for cloning
		private Transcript(TranscriptState state)
		{
			_state = state;
		}

		public Transcript Clone() =>
			new Transcript(_state.Clone());

		public void Statement(Statement statement)
			// TODO add enum for individual tags?
			=> Statement(Encoding.UTF8.GetBytes("Unknown Proof Statement"), statement.PublicPoint, statement.Generators);

		public void Statement(byte[] tag, GroupElement publicPoint, params GroupElement[] generators)
			=> Statement(tag, publicPoint, generators as IEnumerable<GroupElement>);

		public void Statement(byte[] tag, GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			var concatenation = generators.SelectMany(x => x.ToBytes());
			var hash = ByteHelpers.CombineHash(BitConverter.GetBytes(tag.Length), tag, BitConverter.GetBytes(generators.Count()), concatenation.ToArray());

			_state.AssociatedData(Encoding.UTF8.GetBytes("statement"));
			_state.AssociatedData(hash);
			_state.AssociatedData(publicPoint.ToBytes());
		}

		public Scalar GenerateNonce(Scalar secret, WasabiRandom? random = null)
		{
			return GenerateNonces(new[] { secret }, random)[0];
		}

		// generate synthetic nonce using current state combined with additional randomness
		public Scalar[] GenerateNonces(IEnumerable<Scalar> secrets, WasabiRandom? random = null)
		{
			// to integrate prior inputs for deterministic component of nonce
			// generation, first clone the state at the current point in the
			// transcript, which should already have the statement tag and public
			// inputs committed.
			var forked = _state.Clone();

			// add secret inputs as key material
			forked.Key(secrets.SelectMany(x => x.ToBytes()).ToArray());

			// get randomness from system if no random source specified
			if (random is null)
			{
				random = new SecureRandom();
			}

			// add additional randomness as associated data
			forked.AssociatedData(random.GetBytes(32));

			// generate a new scalar for each secret using this updated state as a seed
			var randomScalars = new Scalar[secrets.Count()];
			for (var i = 0; i < secrets.Count(); i++)
			{
				randomScalars[i] = new Scalar(forked.PRF());
			}

			return randomScalars;
		}

		public void NonceCommitment(GroupElement nonce)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			_state.AssociatedData(Encoding.UTF8.GetBytes("nonce commitment"));
			_state.AssociatedData(nonce.ToBytes());
		}

		// generate Fiat Shamir challenges
		public Scalar GenerateChallenge() =>
			// generate a new scalar using current state as a seed
			new Scalar(_state.PRF());
	}
}
