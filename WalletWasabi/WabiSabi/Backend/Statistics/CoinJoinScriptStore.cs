using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class CoinJoinScriptStore
{
	public CoinJoinScriptStore()
		: this(Enumerable.Empty<Script>())
	{
	}

	public CoinJoinScriptStore(IEnumerable<Script> scripts)
	{
		Scripts = new HashSet<Script>(scripts);
	}

	private HashSet<Script> Scripts { get; } = new();

	public void AddRange(IEnumerable<Script> scripts)
	{
		foreach (var script in scripts)
		{
			Scripts.Add(script);
		}
	}

	public bool Contains(Script script)
	{
		return Scripts.Contains(script);
	}

	public static CoinJoinScriptStore LoadFromFile(string filePath)
	{
		var scripts = File.Exists(filePath)
			? File.ReadAllLines(filePath).Select(x => Script.FromHex(x))
			: Enumerable.Empty<Script>();

		CoinJoinScriptStore coinJoinScriptStore = new(scripts);
		return coinJoinScriptStore;
	}
}
