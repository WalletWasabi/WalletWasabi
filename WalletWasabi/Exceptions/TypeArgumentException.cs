namespace WalletWasabi.Exceptions;

public class TypeArgumentException : ArgumentException
{
	public TypeArgumentException(object value, Type expected, string paramName) : base($"Invalid type: {value.GetType()}. Expected: {expected}.", paramName)
	{
	}
}
