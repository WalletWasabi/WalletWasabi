using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public struct InOutInfoTuple : IEquatable<InOutInfoTuple>
	{
		public InOutInfoTuple(AddressMoneyTuple input, AddressMoneyTuple output)
		{
			Input = input;
			Output = output;
		}
		public AddressMoneyTuple Input { get; }
		public AddressMoneyTuple Output { get; }
		public static bool operator ==(InOutInfoTuple x, InOutInfoTuple y) => x.Equals(y);
		public static bool operator !=(InOutInfoTuple x, InOutInfoTuple y) => !(x == y);

		public bool Equals(InOutInfoTuple other)
		{
			return this.Input == other.Input 
				&& this.Output == other.Output;
		}
	}
}
