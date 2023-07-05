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
	private Dictionary<ScriptPubKeyType, Dictionary<bool, List<SynchronizationInfos>>> SynchronizationInfosGrouped { get; } = new();

	private HdPubKeyGlobalView Snapshot =>
		new(this.ToImmutableList());

	public Dictionary<ScriptPubKeyType, Dictionary<bool, List<SynchronizationInfos>>> GetSynchronizationInfosGrouped()
	{
		return SynchronizationInfosGrouped;
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
		AddToSynchronizationInfosGrouped(scriptPubKey, hdPubKey);
		HdPubKeysByScript.AddOrReplace(scriptPubKey, hdPubKey);
		HdPubKeys.Add(hdPubKey);
	}

	private void AddToSynchronizationInfosGrouped(Script scriptPubKey, HdPubKey hdPubKey)
	{
		var toAddItem = new SynchronizationInfos(hdPubKey.FullKeyPath, new ScriptBytesHdPubKeyPair(scriptPubKey.ToCompressedBytes(), hdPubKey));
		var scriptType = hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath();
		if (SynchronizationInfosGrouped.TryGetValue(scriptType, out var scriptTypeGroup))
		{
			if (scriptTypeGroup.TryGetValue(hdPubKey.IsInternal, out var isInternalGroup))
			{
				isInternalGroup.Add(toAddItem);
			}
			else
			{
				scriptTypeGroup.Add(hdPubKey.IsInternal, new List<SynchronizationInfos>() { toAddItem });
			}
		}
		else
		{
			var toAddDictionary = new Dictionary<bool, List<SynchronizationInfos>> { { hdPubKey.IsInternal, new List<SynchronizationInfos> () { toAddItem } } };
			SynchronizationInfosGrouped.Add(scriptType, toAddDictionary);
		}
	}

	public IEnumerator<HdPubKey> GetEnumerator() =>
		HdPubKeys
		.OrderBy(x => x.Index)
		.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public record ScriptBytesHdPubKeyPair(byte[] ScriptBytes, HdPubKey HdPubKey);
	public record SynchronizationInfos(KeyPath KeyPath, ScriptBytesHdPubKeyPair ScriptBytesHdPubKeyPair);
}
