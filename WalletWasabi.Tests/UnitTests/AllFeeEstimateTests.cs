using NBitcoin.RPC;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class AllFeeEstimateTests
	{
		[Fact]
		public void AllFeeEstimateDeserialization()
		{
			var serialized =
				"{"
				+ "\"allFeeEstimate\": {"
					+ "\"type\": 0,"
					+ "\"isAccurate\": true,"
					+ "\"estimations\": {"
						+ "\"2\": 32,"
						+ "\"3\": 30,"
						+ "\"4\": 28,"
						+ "\"6\": 24,"
						+ "\"8\": 5,"
						+ "\"10\": 4,"
						+ "\"73\": 3,"
						+ "\"145\": 2,"
						+ "\"505\": 1"
						+ "}"
					+ "}"
				+ "}";

			var deserialized = JsonConvert.DeserializeObject<SynchronizeResponse>(serialized, new FeeRatePerKbJsonConverter());
			JsonConvert.SerializeObject(deserialized,  new FeeRatePerKbJsonConverter());

		}


		[Fact]
		public void AllFeeEstimateSerialization()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(102m) },
				{ 3, new FeeRate(20m) },
				{ 19, new FeeRate(1m) }
			};
			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			var serialized = JsonConvert.SerializeObject(allFee);
			var deserialized = JsonConvert.DeserializeObject<AllFeeEstimate>(serialized, new FeeRatePerKbJsonConverter());

			Assert.Equal(estimations[2], deserialized.Estimations[2]);
			Assert.Equal(estimations[3], deserialized.Estimations[3]);
			Assert.Equal(estimations[19], deserialized.Estimations[19]);
			Assert.Equal(EstimateSmartFeeMode.Conservative, deserialized.Type);
		}

		[Fact]
		public void AllFeeEstimateOrdersByTarget()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 3, new FeeRate(20m) },
				{ 2, new FeeRate(102m) },
				{ 19, new FeeRate(1m) }
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[3], allFee.Estimations[3]);
			Assert.Equal(estimations[19], allFee.Estimations[19]);
		}

		[Fact]
		public void AllFeeEstimateHandlesDuplicate()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(20m) },
				{ 3, new FeeRate(20m) },
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
		}

		[Fact]
		public void AllFeeEstimateHandlesInconsistentData()
		{
			var estimations = new Dictionary<int, FeeRate>
			{
				{ 2, new FeeRate(20m) },
				{ 3, new FeeRate(21m) },
			};

			var allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Single(allFee.Estimations);
			Assert.Equal(estimations[2], allFee.Estimations[2]);

			estimations = new Dictionary<int, FeeRate>
			{
				{ 5, new FeeRate(1_000m) },
				{ 3, new FeeRate(21m) },
				{ 2, new FeeRate(20m) },
				{ 100, new FeeRate(100m) },
				{ 4, new FeeRate(4m) },
			};

			allFee = new AllFeeEstimate(EstimateSmartFeeMode.Conservative, estimations, true);
			Assert.Equal(2, allFee.Estimations.Count);
			Assert.Equal(estimations[2], allFee.Estimations[2]);
			Assert.Equal(estimations[4], allFee.Estimations[4]);
		}
	}
}
