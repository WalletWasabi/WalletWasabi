using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class UtxoRefereeTests
	{
		[Fact]
		public void UtxoRefereeSerialization()
		{
			var record = BannedUtxo.FromString("2018-11-23 15-23-14:1:44:2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492:True:195");

			Assert.Equal(new DateTimeOffset(2018, 11, 23, 15, 23, 14, TimeSpan.Zero), record.TimeOfBan);
			Assert.Equal(1, record.Severity);
			Assert.Equal(44u, record.Utxo.N);
			Assert.Equal(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), record.Utxo.Hash);
			Assert.True(record.IsNoted);
			Assert.Equal(195, record.BannedForRound);

			DateTimeOffset dateTime = DateTimeOffset.UtcNow;
			DateTimeOffset now = new DateTimeOffset(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
			var record2Init = new BannedUtxo(record.Utxo, 3, now, false, 99);
			string record2Line = record2Init.ToString();
			var record2 = BannedUtxo.FromString(record2Line);

			Assert.Equal(now, record2.TimeOfBan);
			Assert.Equal(3, record2.Severity);
			Assert.Equal(44u, record2.Utxo.N);
			Assert.Equal(new uint256("2716e680f47d74c1bc6f031da22331564dd4c6641d7216576aad1b846c85d492"), record2.Utxo.Hash);
			Assert.False(record2.IsNoted);
			Assert.Equal(99, record2.BannedForRound);
		}
	}
}
