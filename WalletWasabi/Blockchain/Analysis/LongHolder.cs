using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.Analysis;

/// <summary>
/// Helper class that holds a long, but intentionally does not implement equality and comparability.
/// </summary>
public class LongHolder
{
	public LongHolder(long l)
	{
		Long = l;
	}

	public long Long { get; }

	public override string ToString() => Long.ToString();
}
