namespace System.Collections.Generic
{
	public static class ListExtensions
	{
		public static void RemoveFirst<T>(this List<T> me)
		{
			me.RemoveAt(0);
		}

		public static void RemoveLast<T>(this List<T> me)
		{
			me.RemoveAt(me.Count - 1);
		}
	}
}
