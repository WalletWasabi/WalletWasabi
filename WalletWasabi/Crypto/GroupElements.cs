using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class GroupElements : IEnumerable<GroupElement>
	{
		public GroupElements(IEnumerable<GroupElement> groupElements)
		{
			Guard.NotNull(nameof(groupElements), groupElements);
			InnerGroupElements = groupElements.ToArray();
		}

		private IEnumerable<GroupElement> InnerGroupElements { get; }

		public IEnumerator<GroupElement> GetEnumerator() =>
			InnerGroupElements.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();

		public int Count => InnerGroupElements.Count();
	}
}
