using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyCache : IEnumerable<HdPubKey>
{
	private Dictionary<Script, HdPubKey> HdPubKeysByScript { get; } = new();
	private HashSet<HdPubKey> HdPubKeys { get; } = new();
	private Dictionary<KeyPath, ScriptBytesHdPubKeyPair> ScriptBytesHdPubKeyPairByKeyPath { get; } = new();

	private HdPubKeyGlobalView Snapshot =>
		new(this.ToImmutableList());

	/// <summary>
	/// Gets scriptPubKeys snapshot unless the caller already has an up-to-date snapshot of the list.
	/// </summary>
	/// <param name="snapshotId">Number of elements in the snapshot the caller has.</param>
	/// <param name="pubKeys">If new scriptPubKeys were added to the <see cref="HdPubKeyCache"/>, then this contains the new list of scriptPubKeys.</param>
	/// <remarks>The method requires <see cref="HdPubKeyCache"/> to be an add-only cache.</remarks>
	public bool TryGetSynchronizationSnapshot(long snapshotId, [NotNullWhen(true)] out IImmutableList<ScriptBytesHdPubKeyPair>? pubKeys)
	{
		if (snapshotId < HdPubKeys.Count)
		{
			pubKeys = ScriptBytesHdPubKeyPairByKeyPath.Values.ToImmutableList();
			return true;
		}

		pubKeys = null;
		return false;
	}
	
	public bool TryGetPubKey(Script destination, [NotNullWhen(true)] out HdPubKey? hdPubKey) =>
		HdPubKeysByScript.TryGetValue(destination, out hdPubKey);

	public HdPubKeyPathView GetView(KeyPath keyPath) =>
		Snapshot.GetChildKeyOf(keyPath);

	public IEnumerable<HdPubKey> AddRangeKeys(IEnumerable<HdPubKey> keys)
	{
		foreach (var key in keys)
		{
			AddKey(key, key.FullKeyPath.GetScriptTypeFromKeyPath());
		}

		return keys;
	}

	public void AddKey(HdPubKey hdPubKey, ScriptPubKeyType scriptPubKeyType)
	{
		var scriptPubKey = hdPubKey.PubKey.GetScriptPubKey(scriptPubKeyType);
		HdPubKeysByScript.AddOrReplace(scriptPubKey, hdPubKey);
		ScriptBytesHdPubKeyPairByKeyPath.AddOrReplace(hdPubKey.FullKeyPath, new ScriptBytesHdPubKeyPair(scriptPubKey.ToCompressedBytes(), hdPubKey));
		HdPubKeys.Add(hdPubKey);
	}

	public IEnumerator<HdPubKey> GetEnumerator() =>
		HdPubKeys
		.OrderBy(x => x.Index)
		.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public record ScriptBytesHdPubKeyPair(byte[] ScriptBytes, HdPubKey HdPubKey);
}
