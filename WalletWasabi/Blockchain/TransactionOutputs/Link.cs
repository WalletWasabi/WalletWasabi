using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionOutputs
{
	public enum LinkType
	{
		Spends,
		SpentBy,
		SameScriptPubKey,
		SamePubKey
	}

	public class ILink
	{
		SmartCoin Coin { get; }
		LinkType LinkType { get; }
	}

	public class CoinLink : ILink
	{
		public SmartCoin Coin { get; }
		public SmartCoin TargetCoin { get; }
		public LinkType LinkType { get; }

		public CoinLink(SmartCoin sourceCoin, SmartCoin targetCoin, LinkType linkType)
		{
			Coin = sourceCoin;
			TargetCoin = targetCoin;
			LinkType = linkType;
		}
	}
}
