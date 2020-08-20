using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class TranscriptTests
	{
		[Fact]
		public void BuildThrows()
		{
			var tag = Encoding.UTF8.GetBytes("statement tag");

			var t = new Transcript();

			// Demonstrate when it shouldn't throw.
			t.Statement(new Statement(Generators.G, Generators.Ga));
			t.Statement(new Statement(Generators.G, Generators.Ga, Generators.Gg, Generators.Gh));

			// Infinity cannot pass through.
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(Generators.G, GroupElement.Infinity)));
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(GroupElement.Infinity, Generators.Ga)));
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(GroupElement.Infinity, GroupElement.Infinity)));

			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(GroupElement.Infinity, Generators.Ga, Generators.Gg, Generators.Gh)));
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(Generators.G, GroupElement.Infinity, Generators.Gg, Generators.Gh)));
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(Generators.G, Generators.Ga, GroupElement.Infinity, Generators.Gh)));
			Assert.ThrowsAny<ArgumentException>(() => t.Statement(new Statement(Generators.G, Generators.Ga, Generators.Gg, GroupElement.Infinity)));
		}

		[Fact]
		public void FiatShamir()
		{
			Encoding.UTF8.GetBytes("statement tag");

			var p = new Transcript();
			p.Statement(new Statement(Generators.G, Generators.Ga));
			p.GenerateNonce(Scalar.One);
			p.NonceCommitment(Generators.Gg);

			var v = new Transcript();
			v.Statement(new Statement(Generators.G, Generators.Ga));
			v.NonceCommitment(Generators.Gg);

			Assert.Equal(p.GenerateChallenge(), v.GenerateChallenge());
		}

		[Fact]
		public void FiatShamirClone()
		{
			Encoding.UTF8.GetBytes("statement tag");

			var a = new Transcript();
			a.Statement(new Statement(Generators.G, Generators.Gh)); // set up some initial state
			var b = a.Clone();

			a.Statement(new Statement(Generators.G, Generators.Ga));

			b.Statement(new Statement(Generators.G, Generators.Ga));

			Assert.Equal(a.GenerateChallenge(), b.GenerateChallenge());
		}

		[Fact]
		public void FiatShamirNonces()
		{
			// ensure nonce generation does not cause divergence
			Encoding.UTF8.GetBytes("statement tag");

			var a = new Transcript();
			a.Statement(new Statement(Generators.G, Generators.Ga));

			var mra = new MockRandom();
			var rnd1 = new byte[32];
			rnd1[0] = 42;
			mra.GetBytesResults.Add(rnd1);

			var b = new Transcript();
			b.Statement(new Statement(Generators.G, Generators.Ga));

			var mrb = new MockRandom();

			var rnd2 = new byte[32];
			rnd2[0] = 43;
			mrb.GetBytesResults.Add(rnd2);

			Assert.NotEqual(a.GenerateNonce(Scalar.One, mra), b.GenerateNonce(Scalar.One, mrb));
			Assert.Equal(a.GenerateChallenge(), b.GenerateChallenge());
		}

		[Fact]
		public void SyntheticNoncesSecretDependence()
		{
			Encoding.UTF8.GetBytes("statement tag");

			var a = new Transcript();
			a.Statement(new Statement(Generators.G, Generators.Ga));

			var mra = new MockRandom();
			mra.GetBytesResults.Add(new byte[32]);
			mra.GetBytesResults.Add(new byte[32]);

			var b = new Transcript();
			b.Statement(new Statement(Generators.G, Generators.Ga));

			var mrb = new MockRandom();
			mrb.GetBytesResults.Add(new byte[32]);
			mrb.GetBytesResults.Add(new byte[32]);

			Assert.Equal(a.GenerateNonce(Scalar.One, mra), b.GenerateNonce(Scalar.One, mrb));
			Assert.NotEqual(a.GenerateNonce(Scalar.Zero, mra), b.GenerateNonce(Scalar.One, mrb));
		}

		[Fact]
		public void SyntheticNoncesPublicDependence()
		{
			Encoding.UTF8.GetBytes("statement tag");

			var a = new Transcript();
			a.Statement(new Statement(Generators.G, Generators.Ga));

			var mra = new MockRandom();
			mra.GetBytesResults.Add(new byte[32]);

			var b = new Transcript();
			b.Statement(new Statement(Generators.Gg, Generators.Ga));

			var mrb = new MockRandom();
			mrb.GetBytesResults.Add(new byte[32]);

			Assert.NotEqual(a.GenerateNonce(Scalar.One, mra), b.GenerateNonce(Scalar.One, mrb));
		}

		[Fact]
		public void SyntheticNoncesGeneratorDependence()
		{
			Encoding.UTF8.GetBytes("statement tag");

			var a = new Transcript();
			a.Statement(new Statement(Generators.G, Generators.Ga));

			var mra = new MockRandom();
			mra.GetBytesResults.Add(new byte[32]);

			var b = new Transcript();
			b.Statement(new Statement(Generators.G, Generators.Gg));

			var mrb = new MockRandom();
			mrb.GetBytesResults.Add(new byte[32]);

			Assert.NotEqual(a.GenerateNonce(Scalar.One, mra), b.GenerateNonce(Scalar.One, mrb));
		}

		[Fact]
		public void SyntheticNoncesStatementDependence()
		{
			var tag1 = Encoding.UTF8.GetBytes("statement tag");
			var tag2 = Encoding.UTF8.GetBytes("statement tga");

			var a = new Transcript();
			a.Statement(tag1, Generators.G, Generators.Ga);

			var mra = new MockRandom();
			mra.GetBytesResults.Add(new byte[32]);

			var b = new Transcript();
			b.Statement(tag2, Generators.G, Generators.Ga);

			var mrb = new MockRandom();
			mrb.GetBytesResults.Add(new byte[32]);

			Assert.NotEqual(a.GenerateNonce(Scalar.One, mra), b.GenerateNonce(Scalar.One, mrb));
		}
	}
}
