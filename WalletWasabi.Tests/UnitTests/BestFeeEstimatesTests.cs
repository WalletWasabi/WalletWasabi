using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class BestFeeEstimatesTests
	{
		[Fact]
		public void AllFeeEstimateSerialization()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 102 },
				{ 3, 20 },
				{ 19, 1 }
			};
			var allFee = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			var serialized = JsonConvert.SerializeObject(allFee);
			var deserialized = JsonConvert.DeserializeObject<BestFeeEstimates>(serialized);

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[36]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void AllFeeEstimateOrdersByTarget()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 3, 20 },
				{ 2, 102 },
				{ 19, 1 }
			};

			var allFee = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[3], allFee.Estimations[3]);
			Assert.Equal(estimations[19], allFee.Estimations[36]);
		}

		[Fact]
		public void AllFeeEstimateHandlesDuplicate()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 20 }
			};

			var allFee = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
		}

		[Fact]
		public void AllFeeEstimateHandlesInconsistentData()
		{
			var estimations = new Dictionary<int, int>
			{
				{ 2, 20 },
				{ 3, 21 }
			};

			var allFee = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);

			estimations = new Dictionary<int, int>
			{
				{ 18, 1000 },
				{ 3, 21 },
				{ 2, 20 },
				{ 100, 100 },
				{ 6, 4 },
			};

			allFee = new BestFeeEstimates(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[6], allFee.Estimations[6]);
		}
	}
}
