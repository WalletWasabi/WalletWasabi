using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

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

		private string _label;

		[JsonProperty(Order = 3)]
		public string Label
		{
			get => _label;
			private set
			{
				value = Guard.Correct(value);
				if (value != _label)
				{
					_label = value;
					HasLabel = !string.IsNullOrEmpty(value);
				}
			}
		}

		[JsonProperty(Order = 4)]
		public KeyState KeyState { get; private set; }

		public HdPubKey(PubKey pubKey, KeyPath fullKeyPath, string label, KeyState keyState)
		{
			PubKey = Guard.NotNull(nameof(pubKey), pubKey);
			FullKeyPath = Guard.NotNull(nameof(fullKeyPath), fullKeyPath);
			SetLabel(label, null);
			KeyState = keyState;

			P2pkScript = PubKey.ScriptPubKey;
			P2pkhScript = PubKey.Hash.ScriptPubKey;
			P2wpkhScript = PubKey.WitHash.ScriptPubKey;
			P2shOverP2wpkhScript = P2wpkhScript.Hash.ScriptPubKey;

			PubKeyHash = PubKey.Hash;
			HashCode = PubKeyHash.GetHashCode();

			Index = (int)FullKeyPath.Indexes[4];
			NonHardenedKeyPath = new KeyPath(FullKeyPath[3], FullKeyPath[4]);

			int change = (int)FullKeyPath.Indexes[3];
			if (change == 0)
			{
				IsInternal = false;
			}
			else if (change == 1)
			{
				IsInternal = true;
			}
			else
			{
				throw new ArgumentException(nameof(FullKeyPath));
			}
		}

		public void SetLabel(string label, KeyManager kmToFile = null)
		{
			label = Guard.Correct(label);
			if (Label == label)
			{
				return;
			}

			Label = label;

			kmToFile?.ToFile();
		}

		public void SetKeyState(KeyState state, KeyManager kmToFile = null)
		{
			if (KeyState == state)
			{
				return;
			}

			KeyState = state;

			kmToFile?.ToFile();
		}

		public Script P2pkScript { get; }
		public Script P2pkhScript { get; }
		public Script P2wpkhScript { get; }
		public Script P2shOverP2wpkhScript { get; }

		public KeyId PubKeyHash { get; }

		public int Index { get; }
		public KeyPath NonHardenedKeyPath { get; }
		public bool IsInternal { get; }

		public bool HasLabel { get; private set; }

		public BitcoinPubKeyAddress GetP2pkhAddress(Network network) => (BitcoinPubKeyAddress)PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

		public BitcoinWitPubKeyAddress GetP2wpkhAddress(Network network) => PubKey.GetSegwitAddress(network);

		public BitcoinScriptAddress GetP2shOverP2wpkhAddress(Network network) => P2wpkhScript.GetScriptAddress(network);

		#region Equality

		public override bool Equals(object obj) => obj is HdPubKey && this == (HdPubKey)obj;

		public bool Equals(HdPubKey other) => this == other;

		private int HashCode { get; }

		public override int GetHashCode() => HashCode;

		public static bool operator ==(HdPubKey x, HdPubKey y) => x?.PubKeyHash == y?.PubKeyHash;

		public static bool operator !=(HdPubKey x, HdPubKey y) => !(x == y);

		#endregion Equality
	}
}
