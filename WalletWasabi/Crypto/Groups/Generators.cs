using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Crypto.Groups
{
	public static class Generators
	{
		/// <summary>
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

		public static bool TryGetFriendlyGeneratorName(GroupElement ge, out string name)
		{
			name = "";

			if (ge.X == G.X && ge.Y == G.Y)
			{
				name = "Standard";
			}
			else if (ge.X == Gw.X && ge.Y == Gw.Y)
			{
				name = nameof(Gw);
			}
			else if (ge.X == Gwp.X && ge.Y == Gwp.Y)
			{
				name = nameof(Gwp);
			}
			else if (ge.X == Gx0.X && ge.Y == Gx0.Y)
			{
				name = nameof(Gx0);
			}
			else if (ge.X == Gx1.X && ge.Y == Gx1.Y)
			{
				name = nameof(Gx1);
			}
			else if (ge.X == GV.X && ge.Y == GV.Y)
			{
				name = nameof(GV);
			}
			else if (ge.X == Gg.X && ge.Y == Gg.Y)
			{
				name = nameof(Gg);
			}
			else if (ge.X == Gh.X && ge.Y == Gh.Y)
			{
				name = nameof(Gh);
			}
			else if (ge.X == Ga.X && ge.Y == Ga.Y)
			{
				name = nameof(Ga);
			}
			else if (ge.X == Gs.X && ge.Y == Gs.Y)
			{
				name = nameof(Gs);
			}
			else
			{
				return false;
			}

			name += " Generator";

			return true;
		}
	}
}
