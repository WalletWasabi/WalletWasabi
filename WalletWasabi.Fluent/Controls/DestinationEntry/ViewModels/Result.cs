using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

	[DisallowNull]
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

	protected bool Equals(Result<T> other)
	{
		return EqualityComparer<T>.Default.Equals(Value, other.Value);
	}

	public override bool Equals(object? obj)
	{
		if (obj is null)
		{
			return false;
		}

		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj.GetType() != GetType())
		{
			return false;
		}

		return Equals((Result<T>) obj);
	}

	public override int GetHashCode()
	{
		return EqualityComparer<T>.Default.GetHashCode(Value);
	}
}
