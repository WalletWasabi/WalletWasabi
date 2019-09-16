using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models;

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
		[JsonConverter(typeof(SmartLabelJsonConverter))]
		public SmartLabel Label { get; private set; }

		[JsonProperty(Order = 4)]
		public KeyState KeyState { get; private set; }

		public HdPubKey(PubKey pubKey, KeyPath fullKeyPath, SmartLabel label, KeyState keyState)
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

		public void SetLabel(SmartLabel label, KeyManager kmToFile = null)
		{
			label = label ?? SmartLabel.Empty;

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

		public BitcoinPubKeyAddress GetP2pkhAddress(Network network) => (BitcoinPubKeyAddress)PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

		public BitcoinWitPubKeyAddress GetP2wpkhAddress(Network network) => PubKey.GetSegwitAddress(network);

		public BitcoinScriptAddress GetP2shOverP2wpkhAddress(Network network) => P2wpkhScript.GetScriptAddress(network);

		public bool ContainsScript(Script scriptPubKey)
		{
			var scripts = new[]
			{
				P2pkScript,
				P2pkhScript,
				P2wpkhScript,
				P2shOverP2wpkhScript
			};

			return scripts.Contains(scriptPubKey);
		}

		#region Equality

		public override bool Equals(object obj) => obj is HdPubKey pubKey && this == pubKey;

		public bool Equals(HdPubKey other) => this == other;

		private int HashCode { get; }

		public override int GetHashCode() => HashCode;

		public static bool operator ==(HdPubKey x, HdPubKey y) => x?.PubKeyHash == y?.PubKeyHash;

		public static bool operator !=(HdPubKey x, HdPubKey y) => !(x == y);

		#endregion Equality
	}
}
