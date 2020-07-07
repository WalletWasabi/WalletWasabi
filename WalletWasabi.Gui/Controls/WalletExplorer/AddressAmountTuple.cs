using NBitcoin;
using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public readonly struct AddressAmountTuple : IEquatable<AddressAmountTuple>
	{
		public AddressAmountTuple(string address = default(string), Money amount = default(Money), bool isEmpty = true)
		{
			Address = address;
			Amount = amount;
			IsEmpty = isEmpty;
		}

		public string Address { get; }
		public Money Amount { get; }
		private bool IsEmpty { get; }

		public static bool operator ==(AddressAmountTuple x, AddressAmountTuple y) => x.Equals(y);
		public static bool operator !=(AddressAmountTuple x, AddressAmountTuple y) => !(x == y);

		public bool Equals(AddressAmountTuple other) =>
			(IsEmpty, Amount, Address) == (other.IsEmpty, other.Amount, other.Address);

		public override bool Equals(object other) =>
			((AddressAmountTuple)other).Equals(this) == true;

		public override int GetHashCode() =>
			HashCode.Combine(IsEmpty, Amount, Address);
	}
}
