using NBitcoin;
using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
   public readonly struct AddressMoneyTuple : IEquatable<AddressMoneyTuple>
	{
		public AddressMoneyTuple(string address = "", Money amount = Money.Zero, bool isEmpty = true)
		{
			Address = address;
			Amount = amount;
			IsEmpty = isEmpty;
		}
		public static AddressMoneyTuple Empty { get; } = new AddressMoneyTuple();

		public string Address { get; }
		public Money Amount { get; }
		private bool IsEmpty { get; }

		public static bool operator ==(AddressMoneyTuple x, AddressMoneyTuple y) => x.Equals(y);
		public static bool operator !=(AddressMoneyTuple x, AddressMoneyTuple y) => !(x == y);

		public bool Equals(AddressMoneyTuple other) =>
			(IsEmpty, Amount, Address)  == (other.IsEmpty, other.Amount, other.Address);

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}
	}


	public override int GetHashCode() =>
		HashCode.Combine(IsEmpty, Amount, Address);
}
