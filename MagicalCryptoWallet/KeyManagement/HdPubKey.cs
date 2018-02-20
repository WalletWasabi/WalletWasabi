using MagicalCryptoWallet.Converters;
using MagicalCryptoWallet.Helpers;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.KeyManagement
{
	[JsonObject(MemberSerialization.OptIn)]
	public class HdPubKey : IEquatable<HdPubKey>
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(PubKeyConverter))]
		public PubKey PubKey { get; }

		[JsonProperty(Order = 2)]
		[JsonConverter(typeof(KeyPathJsonConverter))]
		public KeyPath FullKeyPath { get; }

		[JsonProperty(Order = 3)]
		public string Label { get; set; }
		[JsonProperty(Order = 4)]
		public KeyState KeyState { get; set; }

		public HdPubKey(PubKey pubKey, KeyPath fullKeyPath, string label, KeyState keyState)
		{
			PubKey = Guard.NotNull(nameof(pubKey), pubKey);
			FullKeyPath = Guard.NotNull(nameof(fullKeyPath), fullKeyPath);
			label = label ?? "";
			Label = label.Trim();
			KeyState = keyState;
		}

		private Script _p2pkScript = null;
		public Script GetP2pkScript()
		{
			return _p2pkScript ?? (_p2pkScript = PubKey.ScriptPubKey);
		}

		private Script _p2pkhScript = null;
		public Script GetP2pkhScript()
		{
			return _p2pkhScript ?? (_p2pkhScript = PubKey.Hash.ScriptPubKey);
		}

		private Script _p2wpkhScript = null;
		public Script GetP2wpkhScript()
		{
			return _p2wpkhScript ?? (_p2wpkhScript = PubKey.WitHash.ScriptPubKey);
		}

		private Script _p2shOverP2wpkhScript = null;
		public Script GetP2shOverP2wpkhScript()
		{
			return _p2shOverP2wpkhScript ?? (_p2shOverP2wpkhScript = GetP2wpkhScript().Hash.ScriptPubKey);
		}

		public BitcoinPubKeyAddress GetP2pkhAddress(Network network) => PubKey.GetAddress(network);


		public BitcoinWitPubKeyAddress GetP2wpkhAddress(Network network) => PubKey.GetSegwitAddress(network);


		public BitcoinScriptAddress GetP2shOverP2wpkhAddress(Network network) => GetP2wpkhScript().GetScriptAddress(network);

		private int? _index = null;
		public int GetIndex()
		{
			return (int)(_index ?? (_index = (int)FullKeyPath.Indexes[4]));
		}

		private KeyPath _nonHardenedKeyPath = null;
		public KeyPath GetNonHardenedKeyPath()
		{
			return _nonHardenedKeyPath ?? (_nonHardenedKeyPath = new KeyPath(FullKeyPath[3], FullKeyPath[4]));
		}

		private bool? _isInternal = null;
		public bool IsInternal()
		{
			if (_isInternal == null)
			{
				int change = (int)FullKeyPath.Indexes[3];
				if (change == 0)
				{
					_isInternal = false;
				}
				else if (change == 1)
				{
					_isInternal = true;
				}
				else throw new ArgumentException(nameof(FullKeyPath));
			}
			return (bool)_isInternal;
		}

		#region Equality

		// speedup
		private KeyId _pubKeyHash = null;
		public KeyId GetPubKeyHash()
		{
			return _pubKeyHash ?? (_pubKeyHash = PubKey.Hash);
		}

		public override bool Equals(object obj) => obj is HdPubKey && this == (HdPubKey)obj;
		public bool Equals(HdPubKey other) => this == other;
		// speedup
		private int? _hashCode = null;
		public override int GetHashCode()
		{
			return (int)(_hashCode ?? (_hashCode = PubKey.Hash.GetHashCode()));
		}
		public static bool operator ==(HdPubKey x, HdPubKey y)
		{
			return x.GetPubKeyHash() == y.GetPubKeyHash();
		}
		public static bool operator !=(HdPubKey x, HdPubKey y)
		{
			return !(x == y);
		}

		#endregion
	}
}
