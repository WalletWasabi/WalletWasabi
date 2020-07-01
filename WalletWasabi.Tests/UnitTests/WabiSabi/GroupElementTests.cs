using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi
{
	public class GroupElementTests
	{
		[Fact]
		public void ConstructorTests()
		{
			new GroupElement(GE.Infinity);
			new GroupElement(GE.Zero);

			new GroupElement(new GE(FE.Zero, FE.Zero));
			new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true));
			new GroupElement(new GE(FE.Zero, FE.Zero, infinity: false));

			new GroupElement(new GE(new FE(0), FE.Zero));
			new GroupElement(new GE(new FE(1), FE.Zero));
			new GroupElement(new GE(new FE(uint.MinValue), FE.Zero));
			new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero));

			new GroupElement(new GE(new FE(0), FE.Zero, infinity: true));
			Assert.Throws<ArgumentException>(() => new GroupElement(new GE(new FE(1), FE.Zero, infinity: true)));
			new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: true));
			Assert.Throws<ArgumentException>(() => new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: true)));

			new GroupElement(new GE(new FE(0), FE.Zero, infinity: false));
			new GroupElement(new GE(new FE(1), FE.Zero, infinity: false));
			new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: false));
			new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: false));

			new GroupElement(GEJ.Infinity);

			new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero));
			new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true));
			new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: false));

			new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero));
			new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero));
			new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero));
			new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero));

			new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: true));

			var geInfinity = GE.Infinity;
			var gejInfinity = GEJ.Infinity.ToGroupElement();
			var allZero = new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true).ToGroupElement();
			var a = new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: true).ToGroupElement();
			var b = new GEJ(FE.Zero, new FE(1), FE.Zero, infinity: true).ToGroupElement();
			var c = new GEJ(FE.Zero, FE.Zero, new FE(1), infinity: true).ToGroupElement();
			var ab = new GEJ(new FE(1), new FE(1), FE.Zero, infinity: true).ToGroupElement();
			var ac = new GEJ(new FE(1), FE.Zero, new FE(1), infinity: true).ToGroupElement();
			var bc = new GEJ(FE.Zero, new FE(1), new FE(1), infinity: true).ToGroupElement();
			var abc = new GEJ(new FE(1), new FE(1), new FE(1), infinity: true).ToGroupElement();

			Assert.Throws<ArgumentException>(() => new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: true)));
			Assert.Throws<ArgumentException>(() => new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: true)));
			Assert.Throws<ArgumentException>(() => new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: true)));

			new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: false));
			new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: false));
			new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: false));
			new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: false));
		}

		[Fact]
		public void InfinityTests()
		{
			Assert.True(new GroupElement(GE.Infinity).IsInfinity);
			Assert.False(new GroupElement(GE.Zero).IsInfinity);

			Assert.False(new GroupElement(new GE(FE.Zero, FE.Zero)).IsInfinity);
			Assert.True(new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true)).IsInfinity);
			Assert.False(new GroupElement(new GE(FE.Zero, FE.Zero, infinity: false)).IsInfinity);

			Assert.False(new GroupElement(new GE(new FE(0), FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(1), FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(uint.MinValue), FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero)).IsInfinity);

			Assert.True(new GroupElement(new GE(new FE(0), FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GE(new FE(1), FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: true)).IsInfinity);

			Assert.False(new GroupElement(new GE(new FE(0), FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(1), FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: false)).IsInfinity);

			Assert.True(new GroupElement(GEJ.Infinity).IsInfinity);

			Assert.False(new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero)).IsInfinity);
			Assert.True(new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: false)).IsInfinity);

			Assert.False(new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero)).IsInfinity);

			Assert.True(new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: true)).IsInfinity);
			Assert.True(new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: true)).IsInfinity);

			Assert.False(new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: false)).IsInfinity);
			Assert.False(new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: false)).IsInfinity);
		}

		//[Fact]
		//public void EqualityTests()
		//{
		//	var infinity = new GroupElement(GE.Infinity);
		//	var zero = new GroupElement(GE.Zero);
		//	Assert.NotEqual(infinity, zero);

		//	var zero2 = new GroupElement(new GE(FE.Zero, FE.Zero));
		//	Assert.Equal(zero, zero2);
		//	Assert.NotEqual(infinity, zero2);
		//	new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true));
		//	new GroupElement(new GE(FE.Zero, FE.Zero, infinity: false));

		//	new GroupElement(new GE(new FE(0), FE.Zero));
		//	new GroupElement(new GE(new FE(1), FE.Zero));
		//	new GroupElement(new GE(new FE(uint.MinValue), FE.Zero));
		//	new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero));

		//	new GroupElement(new GE(new FE(0), FE.Zero, infinity: true));
		//	new GroupElement(new GE(new FE(1), FE.Zero, infinity: true));
		//	new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: true));
		//	new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: true));

		//	new GroupElement(new GE(new FE(0), FE.Zero, infinity: false));
		//	new GroupElement(new GE(new FE(1), FE.Zero, infinity: false));
		//	new GroupElement(new GE(new FE(uint.MinValue), FE.Zero, infinity: false));
		//	new GroupElement(new GE(new FE(uint.MaxValue), FE.Zero, infinity: false));

		//	new GroupElement(GEJ.Infinity);

		//	new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero));
		//	new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true));
		//	new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: false));

		//	new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero));
		//	new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero));
		//	new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero));
		//	new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero));

		//	new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: true));
		//	new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: true));
		//	new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: true));
		//	new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: true));

		//	new GroupElement(new GEJ(new FE(0), FE.Zero, FE.Zero, infinity: false));
		//	new GroupElement(new GEJ(new FE(1), FE.Zero, FE.Zero, infinity: false));
		//	new GroupElement(new GEJ(new FE(uint.MinValue), FE.Zero, FE.Zero, infinity: false));
		//	new GroupElement(new GEJ(new FE(uint.MaxValue), FE.Zero, FE.Zero, infinity: false));
		//}
	}
}
