using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.ZeroKnowledge.Transcripting
{
	// implements a stepping stone towards STROBE
	public class TranscriptState
	{
		private byte[] _h;

		public TranscriptState(byte[] initial)
		{
			_h = initial;
		}

		public TranscriptState Clone() =>
			new TranscriptState(_h);

		private void Absorb(StrobeFlags flags, byte[] data) =>
			// modeled after Noise's MixHash operation, in line with reccomendation in
			// per STROBE paper appendix B, for when not using a Sponge function.
			// stepping stone towards STROBE with Keccak.
			_h = ByteHelpers.CombineHash(_h, new[] { (byte)flags }, data);

		// Absorb arbitrary data into the state
		public void AssociatedData(byte[] data) =>
			Absorb(StrobeFlags.A, data);

		// Absorb key material into the state
		public void Key(byte[] newKeyMaterial)
		{
			// is just sha256 enough here instead of HKDF?
			// This only really has implications for synthetic nonces for the moment
			using var hmac1 = new System.Security.Cryptography.HMACSHA256(_h);
			var key1 = hmac1.ComputeHash(newKeyMaterial);
			using var hmac2 = new System.Security.Cryptography.HMACSHA256(key1);
			var key2 = hmac2.ComputeHash(new byte[] { 0x01 }); // note this is just the first iteration of HKDF since there's no change in key size. could also use STROBE flags here?

			// update state to HKDF output
			_h = key2;
			// should this Absorb instead?
			//Absorb(StrobeFlags.A|StrobeFlags.C, key2);
		}

		// Generate pseudo random outputs from state
		public byte[] PRF()
		{
			Absorb(StrobeFlags.I | StrobeFlags.A | StrobeFlags.C, Array.Empty<byte>());

			// only produce chunks of 32 bytes for now
			return ByteHelpers.CombineHash(_h, Encoding.UTF8.GetBytes("PRF output"));
		}
	}
}
