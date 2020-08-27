using Newtonsoft.Json;
using System.Collections.Generic;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class StrobeTestVector
	{
		[JsonProperty(PropertyName = "name")]
		public string Name { get; }
		[JsonProperty(PropertyName = "operations")]
		public List<StrobeOperation> Operations { get; }

		[JsonConstructor]
		public StrobeTestVector(string name, List<StrobeOperation> operations)
		{
			Name = name;
			Operations = operations;
		}
	}
}
