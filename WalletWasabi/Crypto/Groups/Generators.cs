using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Groups
{
	public static class Generators
	{/// <summary>
	 /// The base point defined in the secp256k1 standard used in ECDSA public key derivation.
	 /// </summary>
		public static GroupElement G { get; } = new GroupElement(EC.G);

		/// <summary>
		/// Generator for MAC and Show.
		/// </summary>
		public static GroupElement Gw { get; } = GroupElement.FromText("Gw");

		/// <summary>
		/// Generator for MAC and Show.
		/// </summary>
		public static GroupElement Gwp { get; } = GroupElement.FromText("Gwp");

		/// <summary>
		/// Generator for MAC and Show.
		/// </summary>
		public static GroupElement Gx0 { get; } = GroupElement.FromText("Gx0");

		/// <summary>
		/// Generator for MAC and Show.
		/// </summary>
		public static GroupElement Gx1 { get; } = GroupElement.FromText("Gx1");

		/// <summary>
		/// Generator for MAC and Show.
		/// </summary>
		public static GroupElement GV { get; } = GroupElement.FromText("GV");

		/// <summary>
		/// Generator for Pedersen commitments.
		/// </summary>
		public static GroupElement Gg { get; } = GroupElement.FromText("Gg");

		/// <summary>
		/// Generator for Pedersen commitments.
		/// </summary>
		public static GroupElement Gh { get; } = GroupElement.FromText("Gh");

		/// <summary>
		/// Generator for attributes M_{ai}.
		/// </summary>
		public static GroupElement Ga { get; } = GroupElement.FromText("Ga");

		/// <summary>
		/// Generator for serial numbers.
		/// </summary>
		public static GroupElement Gs { get; } = GroupElement.FromText("Gs");
	}
}
