using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis
{
	/// <summary>
	/// In these tests no inputs, nor outputs in a transaction are controlled by the user.
	/// </summary>
	public class WalletIrrelevantAnonScoreTests
	{
		[Fact]
		public void OneInOneOut()
		{
			var analyser = Common.RandomBlockchainAnalyzer();
			var tx = Common.RandomSmartTransaction(1, 1, 0, 0);

			analyser.Analyze(tx);

			Assert.Empty(tx.WalletInputs);
			Assert.Empty(tx.WalletOutputs);
		}

		[Fact]
		public void ManyInManyOut()
		{
			var analyser = Common.RandomBlockchainAnalyzer();
			var tx = Common.RandomSmartTransaction(3, 3, 0, 0);

			analyser.Analyze(tx);

			Assert.Empty(tx.WalletInputs);
			Assert.Empty(tx.WalletOutputs);
		}
	}
}
