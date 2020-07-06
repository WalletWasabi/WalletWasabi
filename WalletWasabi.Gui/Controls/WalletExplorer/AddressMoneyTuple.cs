using NBitcoin;
using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public struct AddressMoneyTuple : IEquatable<AddressMoneyTuple>
	{
		public AddressMoneyTuple(string address = null, Money amount = null, bool isEmpty = true)
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

		public bool Equals(AddressMoneyTuple other)
		{
			return this.IsEmpty == other.IsEmpty
		 		&& this.Amount == other.Amount
				&& this.Address == other.Address;
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}
	}
}
