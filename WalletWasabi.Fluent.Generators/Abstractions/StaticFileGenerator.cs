using System.Collections.Generic;

namespace WalletWasabi.Fluent.Generators;

internal abstract class StaticFileGenerator
{
	public abstract IEnumerable<(string FileName, string Source)> Generate();
}
