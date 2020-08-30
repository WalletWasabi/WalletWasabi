using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto
{
	public class GroupElementVector : IEnumerable<GroupElement>
	{
		public GroupElementVector(IEnumerable<GroupElement> groupElements)
		{
			GroupElements = groupElements.ToArray();
		}

		private IEnumerable<GroupElement> GroupElements { get; }

		public IEnumerator<GroupElement> GetEnumerator() =>
			GroupElements.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() =>
			GetEnumerator();

		public int Count => GroupElements.Count();

		public GroupElement Sum() =>
			GroupElements.Aggregate(GroupElement.Infinity, (ge, acc) => ge + acc);

		public GroupElement InnerProduct(ScalarVector scalars)
		{
			Guard.NotNull(nameof(scalars), scalars);
			Guard.True(nameof(scalars.Count), scalars.Count == Count);

			var polynome = new GroupElementVector(
				Enumerable
					.Zip(scalars, GroupElements)
					.Select( x => (s: x.First, G: x.Second ))
					.Select( x => x.s * x.G));
			
			return polynome.Sum();
		}
	}
}
