using HiddenWallet.Models;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.WebClients.BlockCypher.Models
{
    public class BlockCypherGeneralInformation
    {
        public string Name { get; set; }
        public Height Height { get; set; }
        public uint256 Hash { get; set; }
        public DateTimeOffset Time { get; set; }
        public Uri LatestUrl { get; set; }
        public uint256 PreviousHash { get; set; }
        public Uri PreviousUrl { get; set; }
        public int PeerCount { get; set; }
        public long UnconfirmedCount { get; set; }
        public FeeRate HighFee { get; set; }
        public FeeRate MediumFee { get; set; }
        public FeeRate LowFee { get; set; }
        public Height LastForkHeight { get; set; }
        public uint256 LastForkHash { get; set; }
    }
}
