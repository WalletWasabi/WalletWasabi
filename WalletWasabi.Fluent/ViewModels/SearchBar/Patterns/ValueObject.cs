// This file belongs to CSharpFunctionalExtensions (https://github.com/vkhorikov/CSharpFunctionalExtensions)
// It's been checked by the owners and collaborators with extensive tests. Therefore, we aren't actively maintaining/editing it to comply with our coding style and standards.

using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;

#nullable disable

[Serializable]
public abstract class ValueObject : IComparable, IComparable<ValueObject>
{
	private int? _cachedHashCode;

	public virtual int CompareTo(object obj)
	{
		var thisType = GetUnproxiedType(this);
		var otherType = GetUnproxiedType(obj);

		if (thisType != otherType)
		{
			return string.Compare(thisType.ToString(), otherType.ToString(), StringComparison.Ordinal);
		}

		var other = (ValueObject)obj;

		var components = GetEqualityComponents().ToArray();
		var otherComponents = other.GetEqualityComponents().ToArray();

		for (var i = 0; i < components.Length; i++)
		{
			var comparison = CompareComponents(components[i], otherComponents[i]);
			if (comparison != 0)
			{
				return comparison;
			}
		}

		return 0;
	}

	public virtual int CompareTo(ValueObject other)
	{
		return CompareTo(other as object);
	}

	public static bool operator ==(ValueObject a, ValueObject b)
	{
		if (a is null && b is null)
		{
			return true;
		}

		if (a is null || b is null)
		{
			return false;
		}

		return a.Equals(b);
	}

	public static bool operator !=(ValueObject a, ValueObject b) => !(a == b);

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}

		if (GetUnproxiedType(this) != GetUnproxiedType(obj))
		{
			return false;
		}

		var valueObject = (ValueObject)obj;

		return GetEqualityComponents().SequenceEqual(valueObject.GetEqualityComponents());
	}

	public override int GetHashCode()
	{
		if (!_cachedHashCode.HasValue)
		{
			_cachedHashCode = GetEqualityComponents()
				.Aggregate(
				1,
				(current, obj) =>
				{
					unchecked
					{
						return (current * 23) + (obj?.GetHashCode() ?? 0);
					}
				});
		}

		return _cachedHashCode.Value;
	}

	protected abstract IEnumerable<object> GetEqualityComponents();

	internal static Type GetUnproxiedType(object obj)
	{
		const string EFCoreProxyPrefix = "Castle.Proxies.";
		const string NHibernateProxyPostfix = "Proxy";

		var type = obj.GetType();
		var typeString = type.ToString();

		if (typeString.Contains(EFCoreProxyPrefix) || typeString.EndsWith(NHibernateProxyPostfix))
		{
			return type.BaseType;
		}

		return type;
	}

	private int CompareComponents(object object1, object object2)
	{
		if (object1 is null && object2 is null)
		{
			return 0;
		}

		if (object1 is null)
		{
			return -1;
		}

		if (object2 is null)
		{
			return 1;
		}

		if (object1 is IComparable comparable1 && object2 is IComparable comparable2)
		{
			return comparable1.CompareTo(comparable2);
		}

		return object1.Equals(object2) ? 0 : -1;
	}
}
