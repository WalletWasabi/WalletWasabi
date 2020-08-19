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
		private State state; // keeps a running hash of the transcript

		public const string DomainSeparator = "WabiSabi v0.0";

		// public constructor always adds domain separator
		public Transcript() =>
			state = new State(Hash(Encoding.UTF8.GetBytes(DomainSeparator)));
		
		// private constructor used for cloning
		private Transcript(State state) =>
			this.state = state;

		public Transcript Clone() =>
			new Transcript(state.Clone());

		// Add a statement identifier tag
		public void Statement(byte[] tag, GroupElement publicPoint, params GroupElement[] generators) =>
			Statement(tag, publicPoint, (IEnumerable<GroupElement>)generators);

		// TODO use Statement object from #4201
		public void Statement(byte[] tag, GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			Guard.False($"{nameof(publicPoint)}.{nameof(publicPoint.IsInfinity)}", publicPoint.IsInfinity);

			// TODO add guard to ensure generators is non empty?
			foreach (var generator in generators)
			{
				Guard.False($"{nameof(generator)}.{nameof(generator.IsInfinity)}", generator.IsInfinity);
			}

			var concatenation = generators.SelectMany(x => x.ToBytes());
			var hash = Hash(BitConverter.GetBytes(tag.Length), tag, BitConverter.GetBytes(generators.Count()), concatenation.ToArray());

			state.AssociatedData(Encoding.UTF8.GetBytes("statement"));
			state.AssociatedData(hash);
			state.AssociatedData(publicPoint.ToBytes());
		}

		// generate synthetic nonce using current state combined with additional randomness
		public Scalar GenerateNonce(Scalar secret, WasabiRandom? random = null)
		{
			random ??= new SecureRandom();

			// to integrate prior inputs for deterministic component of nonce
			// generation, first clone the state at the current point in the
			// transcript, which should already have the statement tag and public
			// inputs committed.
			var forked = state.Clone();

			// add secret inputs as key material
			forked.Key(secret.ToBytes());

			// get randomness from system if no random source specified
			var fromRng = random.GetBytes(32);

			// add additional randomness as associated data
			forked.AssociatedData(fromRng);

			// generate a new scalar using this updated state as a seed
			return new Scalar(forked.PRF());
		}

		public void NonceCommitment(GroupElement nonce)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			state.AssociatedData(Encoding.UTF8.GetBytes("nonce commitment"));
			state.AssociatedData(nonce.ToBytes());
		}

		// generate Fiat Shamir challenges
		public Scalar GenerateChallenge() =>
			// generate a new scalar using current state as a seed
			new Scalar(state.PRF());

		private static byte[] Hash(params byte[][] data)
		{
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			return sha256.ComputeHash(ByteHelpers.Combine(data));
		}

		// implements a stepping stone towards STROBE
		private class State
		{
			private byte[] h;

			public State(byte[] initial) =>
				h = initial;

			public State Clone() =>
				new State(h);

			private void Absorb(StrobeFlags flags, byte[] data) =>
				// modeled after Noise's MixHash operation, in line with reccomendation in
				// per STROBE paper appendix B, for when not using a Sponge function.
				// stepping stone towards STROBE with Keccak.
				h = Hash(h, new[] { (byte)flags }, data);

			// Absorb arbitrary data into the state
			public void AssociatedData(byte[] data) =>
				Absorb(StrobeFlags.A, data);

			// Absorb key material into the state
			public void Key(byte[] newKeyMaterial)
			{
				// is just sha256 enough here instead of HKDF?
				// This only really has implications for synthetic nonces for the moment
				using var hmac1 = new System.Security.Cryptography.HMACSHA256(h);
				var key1 = hmac1.ComputeHash(newKeyMaterial);
				using var hmac2 = new System.Security.Cryptography.HMACSHA256(key1);
				var key2 = hmac2.ComputeHash(new byte[]{ 0x01 }); // note this is just the first iteration of HKDF since there's no change in key size. could also use STROBE flags here?

				// update state to HKDF output
				h = key2;
				// should this Absorb instead?
				//Absorb(StrobeFlags.A|StrobeFlags.C, key2);
			}

			// Generate pseudo random outputs from state
			public byte[] PRF()
			{
				Absorb(StrobeFlags.I | StrobeFlags.A | StrobeFlags.C, new byte[0]);

				// only produce chunks of 32 bytes for now
				return Hash(h, Encoding.UTF8.GetBytes("PRF output"));
			}

			private enum StrobeFlags
			{
				I = 1,
				A = 2,
				C = 4,
				T = 8,
				M = 16,
				K = 32
			}
		}
	}
}
