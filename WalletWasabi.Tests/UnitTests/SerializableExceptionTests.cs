using WalletWasabi.Extensions;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class SerializableExceptionTests
{
	[Fact]
	public void SerializableExceptionTest()
	{
		var message = "Foo Bar Buzz";
		var innerMessage = "Inner Foo Bar Buzz";
		var innerStackTrace = "";

		Exception ex;
		string? stackTrace;
		try
		{
			try
			{
				throw new OperationCanceledException(innerMessage);
			}
			catch (Exception inner)
			{
				innerStackTrace = inner.StackTrace;
				throw new InvalidOperationException(message, inner);
			}
		}
		catch (Exception x)
		{
			stackTrace = x.StackTrace;
			ex = x;
		}

		var serializableException = ex.ToSerializableException();
		var base64string = SerializableException.ToBase64String(serializableException);
		var result = SerializableException.FromBase64String(base64string);

		Assert.Equal(message, result.Message);
		Assert.Equal(stackTrace, result.StackTrace);
		Assert.Equal(typeof(InvalidOperationException).FullName, result.ExceptionType);

		Assert.Equal(innerMessage, result.InnerException?.Message);
		Assert.Equal(innerStackTrace, result.InnerException?.StackTrace);
		Assert.Equal(typeof(OperationCanceledException).FullName, result.InnerException?.ExceptionType);

		var serializableException2 = ex.ToSerializableException();
		Assert.Equal(serializableException, serializableException2);
	}
}
