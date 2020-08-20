using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.Transcripting
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
		public const string DomainSeparator = "WabiSabi v0.0";

		// public constructor always adds domain separator
		public Transcript()
		{
			Hash = ByteHelpers.CombineHash(Encoding.UTF8.GetBytes(DomainSeparator));
		}

		// private constructor used for cloning
		public Transcript(byte[] hash)
		{
			Guard.Same($"{nameof(hash)}{nameof(hash).Length}", 32, hash.Length);
			Hash = hash;
		}

		private byte[] Hash { get; }

		private Transcript Absorb(StrobeFlags flags, byte[] data) =>
			// modeled after Noise's MixHash operation, in line with reccomendation in
			// per STROBE paper appendix B, for when not using a Sponge function.
			// stepping stone towards STROBE with Keccak.
			new Transcript(ByteHelpers.CombineHash(Hash, new[] { (byte)flags }, data));

		// Absorb arbitrary data into the state
		private Transcript AssociatedData(byte[] data) =>
			Absorb(StrobeFlags.A, data);

		// Absorb key material into the state
		private Transcript Key(byte[] newKeyMaterial)
		{
			// is just sha256 enough here instead of HKDF?
			// This only really has implications for synthetic nonces for the moment
			using var hmac1 = new System.Security.Cryptography.HMACSHA256(Hash);
			var key1 = hmac1.ComputeHash(newKeyMaterial);
			using var hmac2 = new System.Security.Cryptography.HMACSHA256(key1);
			var key2 = hmac2.ComputeHash(new byte[] { 0x01 }); // note this is just the first iteration of HKDF since there's no change in key size. could also use STROBE flags here?

			// update state to HKDF output
			return new Transcript(key2);
			// should this Absorb instead?
			//Absorb(StrobeFlags.A|StrobeFlags.C, key2);
		}

		// Generate pseudo random outputs from state
		private (Transcript transcript, byte[] random) PRF()
		{
			var absorbed = Absorb(StrobeFlags.I | StrobeFlags.A | StrobeFlags.C, Array.Empty<byte>());

			// only produce chunks of 32 bytes for now
			return (absorbed, ByteHelpers.CombineHash(absorbed.Hash, Encoding.UTF8.GetBytes("PRF output")));
		}

		public Transcript CommitToStatement(Statement statement)
			=> CommitToStatement(Encoding.UTF8.GetBytes("Unknown Proof Statement"), statement.PublicPoint, statement.Generators); // TODO add enum for individual tags?

		public Transcript CommitToStatement(byte[] tag, GroupElement publicPoint, params GroupElement[] generators)
			=> CommitToStatement(tag, publicPoint, generators as IEnumerable<GroupElement>);

		public Transcript CommitToStatement(byte[] tag, GroupElement publicPoint, IEnumerable<GroupElement> generators)
		{
			var concatenation = generators.SelectMany(x => x.ToBytes());
			var hash = ByteHelpers.CombineHash(BitConverter.GetBytes(tag.Length), tag, BitConverter.GetBytes(generators.Count()), concatenation.ToArray());

			return AssociatedData(Encoding.UTF8.GetBytes("statement"))
				.AssociatedData(hash)
				.AssociatedData(publicPoint.ToBytes());
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
			// add secret inputs as key material
			var forked = Key(secrets.SelectMany(x => x.ToBytes()).ToArray());

			// get randomness from system if no random source specified
			var disposeRandom = false;
			if (random is null)
			{
				random = new SecureRandom();
				disposeRandom = true;
			}

			// add additional randomness as associated data
			forked = forked.AssociatedData(random.GetBytes(32));

			// generate a new scalar for each secret using this updated state as a seed
			var randomScalars = new Scalar[secrets.Count()];
			for (var i = 0; i < secrets.Count(); i++)
			{
				var prf = forked.PRF();
				forked = prf.transcript;
				randomScalars[i] = new Scalar(prf.random);
			}

			if (disposeRandom)
			{
				(random as SecureRandom)?.Dispose();
			}

			return randomScalars;
		}

		public Transcript NonceCommitment(GroupElement nonce)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			return AssociatedData(Encoding.UTF8.GetBytes("nonce commitment"))
				.AssociatedData(nonce.ToBytes());
		}

		// generate Fiat Shamir challenges
		public (Transcript transcript, Scalar random) GenerateChallenge()
		{
			var prf = PRF();
			return (prf.transcript, new Scalar(prf.random)); // generate a new scalar using current state as a seed
		}
	}
}
