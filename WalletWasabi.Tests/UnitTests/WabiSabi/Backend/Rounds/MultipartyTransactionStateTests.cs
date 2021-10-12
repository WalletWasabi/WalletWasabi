using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class MultipartyTransactionStateTests
	{
		[Fact]
		public void CanGetDifferentialStateTest()
		{
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);

			static Coin CreateCoin()
			{
				using var key = new Key();
				return WabiSabiFactory.CreateCoin(key);
			}

			Coin coin1, coin2, coin3;

			// Three events / three states
			var state0 = round.Assert<ConstructionState>();
			var state1 = state0.AddInput(coin1 = CreateCoin());
			var state2 = state1.AddInput(coin2 = CreateCoin());
			var state3 = state2.AddInput(coin3 = CreateCoin());

			// Unknown state. Assumes full state is required
			var diffd30 = state3.GetConstructionStateSince(-1);
			Assert.Equal(state3, diffd30);

			// Only one event is missing
			var diffd32 = state3.GetConstructionStateSince(state2.Order);
			var input = Assert.Single(diffd32.Inputs);
			Assert.Equal(coin3.Outpoint, input.Outpoint);

			// two events are missing
			var diffd31 = state3.GetConstructionStateSince(state1.Order);
			Assert.Collection(diffd31.Inputs,
				x => Assert.Equal(coin2.Outpoint, x.Outpoint),
				x => Assert.Equal(coin3.Outpoint, x.Outpoint));

			// No event is missing (already updated)
			var diffd33 = state3.GetConstructionStateSince(state3.Order);
			Assert.Empty(diffd33.Inputs);

			// Merge initial state0 with full diff. Expected to get state3
			var merged03 = state0.Merge(diffd30);
			Assert.Equal(state3, merged03);

			// Merge state1 with diff between 1 and 3. Expected to get state3
			var merged13 = state1.Merge(diffd31);
			Assert.Equal(state3.Order, merged13.Order);
			Assert.True(merged13.Inputs.SequenceEqual(state3.Inputs));

			//---------------------
			var diff00 = state0.GetConstructionStateSince(0);
			var diff10 = state1.GetConstructionStateSince(0);
			var diff21 = state2.GetConstructionStateSince(1);
			var diff32 = state3.GetConstructionStateSince(2);

			var clientState1 = state1;
			var clientState3 = state3.GetConstructionStateSince(clientState1.Order).MergeBack(clientState1);

			Assert.Equal(state3.Order, clientState3.Order);
			Assert.True(clientState3.Inputs.SequenceEqual(state3.Inputs));
		}
	}
}
