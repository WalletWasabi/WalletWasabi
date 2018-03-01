using System;
using System.Collections.Generic;
using System.Text;

namespace System.Linq
{
	public static class LinqExtensions
	{
		public static T RandomElement<T>(this IEnumerable<T> source)
		{
			T current = default;
			int count = 0;
			foreach (T element in source)
			{
				count++;
				if (new Random().Next(count) == 0)
				{
					current = element;
				}
			}
			if (count == 0)
			{
				return default;
			}
			return current;
		}
	}
}
