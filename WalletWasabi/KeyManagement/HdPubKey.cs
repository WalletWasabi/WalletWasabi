using WalletWasabi.JsonConverters;
using WalletWasabi.Helpers;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;

namespace WalletWasabi.KeyManagement
{
	[JsonObject(MemberSerialization.OptIn)]
	public class HdPubKey : IEquatable<HdPubKey>
	{
		[JsonProperty(Order = 1)]
		[JsonConverter(typeof(PubKeyJsonConverter))]
		public PubKey PubKey { get; }

		[JsonProperty(Order = 2)]
		[JsonConverter(typeof(KeyPathJsonConverter))]
		public KeyPath FullKeyPath { get; }

		[JsonProperty(Order = 3)]
		public string Label { get; private set; }

		[JsonProperty(Order = 4)]
		public KeyState KeyState { get; private set; }

		public HdPubKey(PubKey pubKey, KeyPath fullKeyPath, string label, KeyState keyState)
		{
			PubKey = Guard.NotNull(nameof(pubKey), pubKey);
			FullKeyPath = Guard.NotNull(nameof(fullKeyPath), fullKeyPath);
			Label = Guard.Correct(label);
			KeyState = keyState;

			P2pkScript = PubKey.ScriptPubKey;
			P2pkhScript = PubKey.Hash.ScriptPubKey;
			P2wpkhScript = PubKey.WitHash.ScriptPubKey;
			P2shOverP2wpkhScript = P2wpkhScript.Hash.ScriptPubKey;
		}

		public void SetLabel(string label, KeyManager kmToFile = null)
		{
			label = Guard.Correct(label);
			if (Label == label) return;
			Label = label;

			kmToFile?.ToFile();
		}

		public void SetKeyState(KeyState state, KeyManager kmToFile = null)
		{
			if (KeyState == state) return;
			KeyState = state;

			kmToFile?.ToFile();
		}

		public Script P2pkScript { get; }

		public Script P2pkhScript { get; }

		public Script P2wpkhScript { get; }

		public Script P2shOverP2wpkhScript { get; }

		public BitcoinPubKeyAddress GetP2pkhAddress(Network network) => PubKey.GetAddress(network);

		public BitcoinWitPubKeyAddress GetP2wpkhAddress(Network network) => PubKey.GetSegwitAddress(network);

		public BitcoinScriptAddress GetP2shOverP2wpkhAddress(Network network) => P2wpkhScript.GetScriptAddress(network);

		private int? _index = null;

		public int GetIndex()
		{
			return (int)(_index ?? (_index = (int)FullKeyPath.Indexes[4]));
		}

		private KeyPath _nonHardenedKeyPath = null;

		public KeyPath NonHardenedKeyPath()
		{
			return _nonHardenedKeyPath ?? (_nonHardenedKeyPath = new KeyPath(FullKeyPath[3], FullKeyPath[4]));
		}

		private bool? _isInternal = null;

		public bool IsInternal()
		{
			if (_isInternal is null)
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

		public bool HasLabel() => !string.IsNullOrEmpty(Label);

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
			return x?.GetPubKeyHash() == y?.GetPubKeyHash();
		}

		public static bool operator !=(HdPubKey x, HdPubKey y)
		{
			return !(x == y);
		}

		#endregion Equality
	}
}
