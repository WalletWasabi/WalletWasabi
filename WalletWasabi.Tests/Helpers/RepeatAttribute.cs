using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace WalletWasabi.Tests.Helpers
{
	public sealed class RepeatAttribute : DataAttribute
	{
		private readonly int _count;

		public RepeatAttribute(int count)
		{
			if (count < 1)
			{
				throw new ArgumentOutOfRangeException(
					paramName: nameof(count),
					message: "Repeat count must be greater than 0.");
			}
			_count = count;
		}

		public override IEnumerable<object[]> GetData(MethodInfo testMethod)
			=> Enumerable.Range(1, _count).Select(i => new object[] { i });
	}
}