namespace WalletWasabi.Extensions;

public static class LazyExtensions
{
	public static Lazy<R> Select<T, R>(this Lazy<T> me, Func<T, R> map) =>
		new (() => map(me.Value));
}
