using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Groups
{
	public static class Generators
	{
		/// <summary>
		/// Base point defined in the secp256k1 standard used in ECDSA public key derivation.
		/// </summary>
		public static GroupElement G { get; } = new GroupElement(EC.G);

		/// <summary>
		/// Generator point for MAC and Show.
		/// </summary>
		public static GroupElement Gw { get; } = GroupElement.FromText("Gw");

		/// <summary>
		/// Generator point for MAC and Show.
		/// </summary>
		public static GroupElement Gwp { get; } = GroupElement.FromText("Gwp");

		/// <summary>
		/// Generator point for MAC and Show.
		/// </summary>
		public static GroupElement Gx0 { get; } = GroupElement.FromText("Gx0");

		/// <summary>
		/// Generator point for MAC and Show.
		/// </summary>
		public static GroupElement Gx1 { get; } = GroupElement.FromText("Gx1");

		/// <summary>
		/// Generator point for MAC and Show.
		/// </summary>
		public static GroupElement GV { get; } = GroupElement.FromText("GV");

		/// <summary>
		/// Generator point for Pedersen commitments.
		/// </summary>
		public static GroupElement Gg { get; } = GroupElement.FromText("Gg");

		/// <summary>
		/// Generator point for Pedersen commitments.
		/// </summary>
		public static GroupElement Gh { get; } = GroupElement.FromText("Gh");

		/// <summary>
		/// Generator point for attributes M_{ai}.
		/// </summary>
		public static GroupElement Ga { get; } = GroupElement.FromText("Ga");

		/// <summary>
		/// Generator point for serial numbers.
		/// </summary>
		public static GroupElement Gs { get; } = GroupElement.FromText("Gs");

		public static bool TryGetFriendlyGeneratorName(GroupElement? ge, out string name)
		{
			string formatName (string generatorName) => $"{generatorName} Generator";
			name = ge switch
			{
				_ when ge == G => formatName( "Standard" ),
				_ when ge == Gw => formatName( nameof(Gw) ),
				_ when ge == Gwp => formatName( nameof(Gwp) ),
				_ when ge == Gx0 => formatName( nameof(Gx0) ),
				_ when ge == Gx1 => formatName( nameof(Gx1) ),
				_ when ge == GV => formatName( nameof(GV) ),
				_ when ge == Gg => formatName( nameof(Gg) ),
				_ when ge == Gh => formatName( nameof(Gh) ),
				_ when ge == Ga => formatName( nameof(Ga) ),
				_ when ge == Gs => formatName( nameof(Gs) ),
				_ => ""
			};
			return name != "";
		}
	}
}
