using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabiClientLibrary.Models;
using WalletWasabi.WabiSabiClientLibrary.Models.GetAnonymityScores;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

public class GetAnonymityScoresHelper
{
	public static GetAnonymityScoresResponse GetAnonymityScores(GetAnonymityScoresRequest request)
	{
		TransactionLabelProvider labelProvider = new();
		BlockchainAnalyzer analyser = new();

		List<AnalyzedTransaction> transactions = new();
		AddressAnonymityCollection addressAnonymityCollection = new();

		foreach (var transaction in request.Transactions.Reverse())
		{
			analyser.Analyze(AnalyzedTransaction.FromTransaction(transaction, labelProvider));
			addressAnonymityCollection.AddRange(labelProvider.GetAnonymitySets());
		}

		return new GetAnonymityScoresResponse(addressAnonymityCollection.ToArray());
	}

	private class AddressAnonymityCollection
	{
		public HashSet<string> AddressHashSet { get; }
		public List<AddressAnonymity> AddressAnonymityList { get; }

		public AddressAnonymityCollection()
		{
			AddressHashSet = new();
			AddressAnonymityList = new();
		}

		public void Add(AddressAnonymity addressAnonymity)
		{
			if (!AddressHashSet.Contains(addressAnonymity.Address))
			{
				AddressHashSet.Add(addressAnonymity.Address);
				AddressAnonymityList.Add(addressAnonymity);
			}
		}

		public void AddRange(IEnumerable<AddressAnonymity> addressAnonymityCollection)
		{
			foreach (AddressAnonymity addressAnonymity in addressAnonymityCollection)
			{
				Add(addressAnonymity);
			}
		}

		public AddressAnonymity[] ToArray()
		{
			return AddressAnonymityList.ToArray();
		}
	}

	private class TransactionLabelProvider
	{
		private Dictionary<string, TransactionLabel> AddressToTransactionLabel { get; }

		public TransactionLabelProvider()
		{
			AddressToTransactionLabel = new();
		}

		public IEnumerable<AddressAnonymity> GetAnonymitySets()
		{
			return AddressToTransactionLabel.Select(x => new AddressAnonymity(x.Key, x.Value.HdPubKey.AnonymitySet));
		}

		public bool TransactionLabelExist(string address)
		{
			return AddressToTransactionLabel.ContainsKey(address);
		}


		public TransactionLabel GetTransactionLabel(string address)
		{
			if (!AddressToTransactionLabel.ContainsKey(address))
			{
				byte[] keyId = NBitcoinHelpers.BetterParseBitcoinAddress(address).ScriptPubKey.ToBytes();
				using Key dummyKey = new(Hashes.SHA256(keyId));
				HdPubKey dummyHdPubKey = new(dummyKey.PubKey, new KeyPath("0/0/0/0/0"), SmartLabel.Empty, KeyState.Clean);
				AddressToTransactionLabel[address] = new TransactionLabel(dummyHdPubKey);
			}

			return AddressToTransactionLabel[address];
		}
	}

	private class TransactionLabel
	{
		public HdPubKey HdPubKey { get; }

		public TransactionLabel(HdPubKey hdPubKey)
		{
			HdPubKey = hdPubKey;
		}
	}

	private class AnalyzedTransaction : SmartTransaction
	{
		static private Network Network { get; } = Network.Main;

		public List<TransactionLabel> InputTransactionLabels { get; }
		public List<TransactionLabel> OutputTransactionLabels { get; }

		private OutPointGenerator OutPointGenerator { get; }

		public AnalyzedTransaction()
			// Network is not used in Transaction.Create
			: base(NBitcoin.Transaction.Create(Network), 0)
		{
			InputTransactionLabels = new List<TransactionLabel>();
			OutputTransactionLabels = new List<TransactionLabel>();
			OutPointGenerator = new();
		}

		public static AnalyzedTransaction FromTransaction(Tx transaction, TransactionLabelProvider transactionLabelProvider)
		{
			AnalyzedTransaction analyzedTransaction = new();

			foreach (var internalInput in transaction.InternalInputs)
			{
				if (!transactionLabelProvider.TransactionLabelExist(internalInput.Address))
				{
					throw new Exception("There is an internal input that references a non-existing transaction.");
				}
				analyzedTransaction.AddInternalInput(internalInput.Value, transactionLabelProvider.GetTransactionLabel(internalInput.Address));
			}

			foreach (var externalInput in transaction.ExternalInputs)
			{
				analyzedTransaction.AddExternalInput();
			}

			foreach (var internalOutput in transaction.InternalOutputs)
			{
				analyzedTransaction.AddInternalOutput(internalOutput.Value, transactionLabelProvider.GetTransactionLabel(internalOutput.Address));
			}

			foreach (var externalOutput in transaction.ExternalOutputs)
			{
				analyzedTransaction.AddExternalOutput(externalOutput.Value, externalOutput.ScriptPubKey);
			}

			if (analyzedTransaction.ForeignInputs.Count + analyzedTransaction.WalletInputs.Count != analyzedTransaction.Transaction.Inputs.Count)
			{
				throw new InvalidOperationException();
			}

			if (analyzedTransaction.ForeignOutputs.Count + analyzedTransaction.WalletOutputs.Count != analyzedTransaction.Transaction.Outputs.Count)
			{
				throw new InvalidOperationException();
			}

			return analyzedTransaction;
		}

		public void AddExternalInput()
		{
			Transaction.Inputs.Add(OutPointGenerator.GenerateOutPoint());
		}

		public void AddExternalOutput(long amountInSatoshis, string scriptPubKey)
		{
			Script script = Script.FromHex(scriptPubKey);
			Transaction.Outputs.Add(new Money(amountInSatoshis, MoneyUnit.Satoshi), script);
		}

		public void AddInternalInput(long amountInSatoshis, TransactionLabel label)
		{
			InputTransactionLabels.Add(label);

			NBitcoin.Transaction previousTransaction = NBitcoin.Transaction.Create(Network);
			previousTransaction.Inputs.Add(OutPointGenerator.GenerateOutPoint());
			previousTransaction.Outputs.Add(new Money(amountInSatoshis), label.HdPubKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86));
			SmartTransaction previousSmartTransaction = new SmartTransaction(previousTransaction, 0);
			SmartCoin smartCoin = new(previousSmartTransaction, 0, label.HdPubKey);

			Transaction.Inputs.Add(smartCoin.Outpoint);
			if (!TryAddWalletInput(smartCoin))
			{
				throw new InvalidOperationException();
			}
		}

		public void AddInternalOutput(long amountInSatoshis, TransactionLabel label)
		{
			OutputTransactionLabels.Add(label);

			Transaction.Outputs.Add(new Money(amountInSatoshis, MoneyUnit.Satoshi), label.HdPubKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86));
			SmartCoin smartCoin = new(this, (uint)Transaction.Outputs.Count - 1, label.HdPubKey);
		}

	}

	private class OutPointGenerator
	{
		private ulong _counter;

		public OutPointGenerator()
		{
			_counter = 0;
		}

		public OutPoint GenerateOutPoint()
		{
			return new OutPoint(new uint256(_counter++), 0);
		}
	}
}

