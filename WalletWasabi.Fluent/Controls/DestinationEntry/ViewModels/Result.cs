namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class Result<T>
{
	private Result(T value)
	{
		Value = value;
		IsSuccess = true;
	}

	private Result(string error)
	{
		Error = error;
		IsFailure = true;
	}

	public T Value { get; }
	public bool IsSuccess { get; }
	public bool IsFailure { get; }
	public string Error { get; }

	public static Result<T> Success(T value)
	{
		return new Result<T>(value);
	}

	public static Result<T> Failure(string error)
	{
		return new Result<T>(error);
	}

	public static implicit operator Result<T>(T value)
	{
		return Success(value);
	}

	public static implicit operator Result<T>(string error)
	{
		return Failure(error);
	}
}