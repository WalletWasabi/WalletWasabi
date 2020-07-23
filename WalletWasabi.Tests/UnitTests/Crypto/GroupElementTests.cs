using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class GroupElementTests
	{
		[Fact]
		public void IsIEquitable()
		{
			Assert.True(GroupElement.Infinity is IEquatable<GroupElement>);
		}

		[Fact]
		public void ConstructorThrows()
		{
			var one = new FE(1);
			var two = new FE(2);
			var large = new FE(uint.MaxValue);
			var largest = new FE(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(FE.Zero, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, one)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(two, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(large, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, largest)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(largest, largest)));

			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, FE.Zero, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, one)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(two, two, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(large, large, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, largest)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(largest, largest, largest)));
		}

		[Fact]
		public void ConstructorDoesntThrow()
		{
			new GroupElement(GE.Infinity);
			new GroupElement(GEJ.Infinity);
			new GroupElement(EC.G);
			new GroupElement(EC.G * new Scalar(1));
			new GroupElement(EC.G * new Scalar(uint.MaxValue));
			new GroupElement(EC.G * new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
		}

		[Fact]
		public void InfinityWorks()
		{
			var a = GroupElement.Infinity;
			var b = new GroupElement(GE.Infinity);
			var c = new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true));
			var d = new GroupElement(new GE(new FE(1), new FE(1), infinity: true));
			var e = new GroupElement(GEJ.Infinity);
			var f = new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true));
			var g = new GroupElement(new GEJ(new FE(1), new FE(1), new FE(1), infinity: true));

			Assert.True(a.IsInfinity);
			Assert.True(b.IsInfinity);
			Assert.True(c.IsInfinity);
			Assert.True(d.IsInfinity);
			Assert.True(e.IsInfinity);
			Assert.True(f.IsInfinity);
			Assert.True(g.IsInfinity);

			Assert.Equal(a, b);
			Assert.Equal(a, c);
			Assert.Equal(a, d);
			Assert.Equal(a, e);
			Assert.Equal(a, f);
			Assert.Equal(a, g);

			Assert.Equal(a.GetHashCode(), b.GetHashCode());
			Assert.Equal(a.GetHashCode(), c.GetHashCode());
			Assert.Equal(a.GetHashCode(), d.GetHashCode());
			Assert.Equal(a.GetHashCode(), e.GetHashCode());
			Assert.Equal(a.GetHashCode(), f.GetHashCode());
			Assert.Equal(a.GetHashCode(), g.GetHashCode());

			var singleSet = new HashSet<GroupElement> { a, b, c, d, e, f, g };
			Assert.Single(singleSet);
		}

		[Fact]
		public void OneEqualsOne()
		{
			var one = new Scalar(1);
			var a = new GroupElement(EC.G * one);
			var b = new GroupElement(EC.G * one);
			Assert.Equal(a, b);
		}

		[Fact]
		public void OneDoesntEqualTwo()
		{
			var one = new Scalar(1);
			var two = new Scalar(2);
			var a = new GroupElement(EC.G * one);
			var b = new GroupElement(EC.G * two);
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void NullEquality()
		{
			var one = new Scalar(1);
			var a = new GroupElement(EC.G * one);

			Assert.False(a == null);
			Assert.True(a != null);

			Assert.False(null == a);
			Assert.True(null != a);

			Assert.False(a.Equals(null));
		}

		[Fact]
		public void InfinityDoesntEqualNotInfinity()
		{
			var one = new Scalar(1);
			var a = new GroupElement(EC.G * one);
			Assert.NotEqual(a, GroupElement.Infinity);
		}

		[Fact]
		public void TransformationsDontRuinEquality()
		{
			var one = new Scalar(1);
			var gej = EC.G * one;
			var ge = gej.ToGroupElement();
			var sameGej = ge.ToGroupElementJacobian();

			var a = new GroupElement(ge);
			var b = new GroupElement(gej);
			var c = new GroupElement(sameGej);

			Assert.Equal(a, b);
			Assert.Equal(a, c);
		}
	}
}
