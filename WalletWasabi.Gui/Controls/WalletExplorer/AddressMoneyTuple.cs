using NBitcoin;
using System;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public struct AddressMoneyTuple : IEquatable<AddressMoneyTuple>
	{
		public AddressMoneyTuple(string address, Money amount)
		{
			Address = address;
			Amount = amount;
			IsEmpty = false;
		}

		private AddressMoneyTuple(bool isEmpty)
		{
			Address = null;
			Amount = null;
			IsEmpty = isEmpty;
		}

		public static AddressMoneyTuple Empty { get; } = new AddressMoneyTuple(true);

		public string Address { get; }
		public Money Amount { get; }
		private bool IsEmpty { get; set; }

		public static bool operator ==(AddressMoneyTuple x, AddressMoneyTuple y) => x.Equals(y);
		public static bool operator !=(AddressMoneyTuple x, AddressMoneyTuple y) => !(x == y);


		public bool Equals(AddressMoneyTuple other)
		{
			return this.IsEmpty == other.IsEmpty
		 		&& this.Amount == other.Amount
				&& this.Address == other.Address;
		}
	}
}
