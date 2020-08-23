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
	// If needed there could be multiple Transcripts sharing a single state,
	// each enforcing a state machine for every sub-proof (statement identifier,
	// public inputs, nonce generation, challenge, responses, synchronized by some
	// parent object.)
	// https://strobe.sourceforge.io/specs/
	public class Transcript
	{
		public const string DomainSeparator = "wabisabi_v1.0";

		// Default constructor always adds domain separator.
		public Transcript()
		{
			Hash = ByteHelpers.CombineHash(Encoding.UTF8.GetBytes(DomainSeparator));
		}

		private Transcript(byte[] hash)
		{
			Guard.Same($"{nameof(hash)}{nameof(hash).Length}", 32, hash.Length);
			Hash = hash;
		}

		private byte[] Hash { get; }

		/// <summary>
		/// Modeled after Noise's MixHash operation, in line with reccomendation in
		/// per STROBE paper appendix B, for when not using a Sponge function.
		/// </summary>
		private Transcript Absorb(StrobeFlags flags, byte[] data) =>
			new Transcript(ByteHelpers.CombineHash(Hash, new[] { (byte)flags }, data));

		/// <summary>
		/// Absorb arbitrary data into the state.
		/// </summary>
		private Transcript AssociateData(byte[] data) =>
			Absorb(StrobeFlags.A, data);

		/// <summary>
		/// Absorb key material into the state.
		/// </summary>
		private Transcript Key(byte[] newKeyMaterial)
		{
			// Is just sha256 enough here instead of HKDF?
			// This only really has implications for synthetic nonces for the moment.
			using var hmac1 = new System.Security.Cryptography.HMACSHA256(Hash);
			var key1 = hmac1.ComputeHash(newKeyMaterial);
			using var hmac2 = new System.Security.Cryptography.HMACSHA256(key1);
			// Note this is just the first iteration of HKDF since there's no change in key size. could also use STROBE flags here?
			var key2 = hmac2.ComputeHash(new byte[] { 0x01 });

			// Update state to HKDF output.
			return new Transcript(key2);
			// Should this Absorb instead?
			// Absorb(StrobeFlags.A|StrobeFlags.C, key2);
		}

		/// <summary>
		/// Generate pseudo random outputs from state.
		/// </summary>
		private (Transcript transcript, byte[] challenge) Prf()
		{
			var absorbed = Absorb(StrobeFlags.I | StrobeFlags.A | StrobeFlags.C, Array.Empty<byte>());

			// Only produce chunks of 32 bytes.
			return (absorbed, ByteHelpers.CombineHash(absorbed.Hash, Encoding.UTF8.GetBytes("prf_output")));
		}

		public Transcript Commit(Statement statement)
		{
			var generators = statement.Generators;
			var concatenation = generators.SelectMany(x => x.ToBytes());
			var hash = ByteHelpers.CombineHash(BitConverter.GetBytes(generators.Count()), concatenation.ToArray());

			return AssociateData(Encoding.UTF8.GetBytes("statement"))
				.AssociateData(hash)
				.AssociateData(statement.PublicPoint.ToBytes());
		}

		public Transcript Commit(GroupElement nonce)
		{
			Guard.False($"{nameof(nonce)}.{nameof(nonce.IsInfinity)}", nonce.IsInfinity);
			return AssociateData(Encoding.UTF8.GetBytes("nonce"))
				.AssociateData(nonce.ToBytes());
		}

		/// <summary>
		/// Generate synthetic nonce using current state combined with additional randomness.
		/// </summary>
		public Scalar GenerateNonce(Scalar secret, WasabiRandom? random = null)
			=> GenerateNonces(new[] { secret }, random).First();

		/// <summary>
		/// Generate synthetic nonces using current state combined with additional randomness.
		/// </summary>
		public Scalar[] GenerateNonces(IEnumerable<Scalar> secrets, WasabiRandom? random = null)
		{
			// To integrate prior inputs for deterministic component of nonce
			// generation, first clone the state at the current point in the
			// transcript, which should already have the statement tag and public
			// inputs committed.
			// Add secret inputs as key material.
			var forked = Key(secrets.SelectMany(x => x.ToBytes()).ToArray());

			// Get randomness from system if no random source specified.
			var disposeRandom = false;
			if (random is null)
			{
				random = new SecureRandom();
				disposeRandom = true;
			}

			// Add additional randomness as associated data.
			forked = forked.AssociateData(random.GetBytes(32));

			// Generate a new scalar for each secret using this updated state as a seed.
			var randomScalars = new Scalar[secrets.Count()];
			for (var i = 0; i < secrets.Count(); i++)
			{
				var challengeGeneration = forked.GenerateChallenge();
				forked = challengeGeneration.transcript;
				randomScalars[i] = challengeGeneration.challenge;
			}

			if (disposeRandom)
			{
				(random as IDisposable)?.Dispose();
			}

			return randomScalars;
		}

		/// <summary>
		/// Generate Fiat Shamir challenges.
		/// </summary>
		public (Transcript transcript, Scalar challenge) GenerateChallenge()
		{
			var prf = Prf();
			return (prf.transcript, new Scalar(prf.challenge)); // Generate a new scalar using current state as a seed.
		}

		public byte[] ToBytes() => Hash;

		public static Transcript FromBytes(byte[] bytes) => new Transcript(bytes);
	}
}
