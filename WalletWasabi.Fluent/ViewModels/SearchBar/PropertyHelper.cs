using System.Linq.Expressions;
using System.Reflection;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public static class PropertyHelper<T>
{
	public static PropertyInfo GetProperty<TValue>(
		Expression<Func<T, TValue>> selector)
	{
		Expression body = selector;
		if (body is LambdaExpression)
		{
			body = ((LambdaExpression)body).Body;
		}

		return body.NodeType switch
		{
			ExpressionType.MemberAccess => (PropertyInfo) ((MemberExpression) body).Member,
			_ => throw new InvalidOperationException()
		};
	}
}