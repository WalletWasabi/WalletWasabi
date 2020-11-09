using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class GroupElementVector : IEnumerable<GroupElement>
	{
		[JsonConstructor]
		internal GroupElementVector(IEnumerable<GroupElement> groupElements)
		{
			Guard.NotNullOrEmpty(nameof(groupElements), groupElements);
			GroupElements = groupElements.ToArray();
		}

		internal GroupElementVector(params GroupElement[] groupElements)
			: this(groupElements as IEnumerable<GroupElement>)
		{
		}

		private IEnumerable<GroupElement> GroupElements { get; }

		public IEnumerator<GroupElement> GetEnumerator() =>
			GroupElements.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();

		public int Count => GroupElements.Count();
	}
}
