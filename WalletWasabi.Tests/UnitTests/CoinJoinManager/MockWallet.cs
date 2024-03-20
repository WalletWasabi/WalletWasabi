using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.CoinJoinManager;

internal class MockWallet : IWallet
{
	public MockWallet(bool isUnderPlebStop)
	{
		IsUnderPlebStop = isUnderPlebStop;
	}

	public string WalletName => throw new NotImplementedException();

	public WalletId WalletId => throw new NotImplementedException();

	public bool IsUnderPlebStop { get; set; }

	public bool IsMixable => throw new NotImplementedException();

	public IKeyChain? KeyChain => throw new NotImplementedException();

	public IDestinationProvider DestinationProvider => throw new NotImplementedException();

	public int AnonScoreTarget => throw new NotImplementedException();

	public bool ConsolidationMode => throw new NotImplementedException();

	public TimeSpan FeeRateMedianTimeFrame => throw new NotImplementedException();

	public bool RedCoinIsolation => throw new NotImplementedException();

	public CoinjoinSkipFactors CoinjoinSkipFactors => throw new NotImplementedException();

	public Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync()
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<SmartTransaction>> GetTransactionsAsync()
	{
		throw new NotImplementedException();
	}

	public Task<bool> IsWalletPrivateAsync()
	{
		throw new NotImplementedException();
	}
}
