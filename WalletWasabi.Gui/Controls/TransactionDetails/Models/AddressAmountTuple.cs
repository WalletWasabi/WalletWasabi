using NBitcoin;
using System;

namespace WalletWasabi.Gui.Controls.TransactionDetails.Models
{
	public readonly struct AddressAmountTuple : IEquatable<AddressAmountTuple>
	{
		public AddressAmountTuple(string address, Money amount)
		{
			Address = address;
			Amount = amount;
		}

		public string Address { get; }
		public Money Amount { get; }

		public static bool operator ==(AddressAmountTuple x, AddressAmountTuple y) => x.Equals(y);

		public static bool operator !=(AddressAmountTuple x, AddressAmountTuple y) => !(x == y);

		public bool Equals(AddressAmountTuple other) =>
			(Amount, Address) == (other.Amount, other.Address);

		public override bool Equals(object other) =>
			((AddressAmountTuple)other).Equals(this) == true;

		public override int GetHashCode() =>
			HashCode.Combine(Amount, Address);
	}
}
