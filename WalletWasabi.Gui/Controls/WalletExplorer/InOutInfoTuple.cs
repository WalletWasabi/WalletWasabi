using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public readonly struct InOutInfoTuple : IEquatable<InOutInfoTuple>
	{
		public InOutInfoTuple(AddressAmountTuple input, AddressAmountTuple output)
		{
			Input = input;
			Output = output;
		}

		public AddressAmountTuple Input { get; }
		public AddressAmountTuple Output { get; }
		public static bool operator ==(InOutInfoTuple x, InOutInfoTuple y) => x.Equals(y);
		public static bool operator !=(InOutInfoTuple x, InOutInfoTuple y) => !(x == y);

		public bool Equals(InOutInfoTuple other) =>
			 (Input, Output) == (other.Input, other.Output);
 
		public override bool Equals(object other) =>
			((InOutInfoTuple)other).Equals(this) == true;

		public override int GetHashCode() =>
			HashCode.Combine(Input, Output);
	}
}
