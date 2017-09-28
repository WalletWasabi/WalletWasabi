using NBitcoin;
using QBitNinja.Client.Models;
using System;
using System.Linq;

namespace HBitcoin.Tests
{
	public class AddressHistoryRecord
	{
		public readonly BalanceOperation Operation;
		public readonly BitcoinAddress Address;

		public AddressHistoryRecord(BitcoinAddress address, BalanceOperation operation)
		{
			Address = address;
			Operation = operation;
		}

		public Money Amount
		{
			get
			{
				var amount = (from Coin coin in Operation.ReceivedCoins
							  let address =
								  coin.GetScriptCode().GetDestinationAddress(Address.Network)
							  where address == Address
							  select coin.Amount).Sum();
				return (from Coin coin in Operation.SpentCoins
						let address =
							coin.GetScriptCode().GetDestinationAddress(Address.Network)
						where address == Address
						select coin)
					.Aggregate(amount, (current, coin) => current - coin.Amount);
			}
		}

		public DateTimeOffset FirstSeen => Operation.FirstSeen;
		public bool Confirmed => Operation.Confirmations > 0;
		public uint256 TransactionId => Operation.TransactionId;
	}
}
