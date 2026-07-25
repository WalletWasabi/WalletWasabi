using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Helpers;

public record Unit
{
	public static readonly Unit Instance = new();
}

public record Result<TValue,TError>
{
	private readonly TValue? _value;
	private readonly TError? _error;

	protected Result(TValue value)
	{
		IsOk = true;
		_value = value;
		_error = default;
	}

	protected Result(TError error)
	{
		IsOk = false;
		_value = default;
		_error = error;
	}

	public static Result<TValue, TError> Ok(TValue value) => value;
	public static Result<TValue, TError> Fail(TError error) => error;
	public static implicit operator Result<TValue, TError>(TValue value) => new(value);
	public static implicit operator Result<TValue, TError> (TError error) => new(error);

	public delegate T SuccessAction<out T>(TValue s);
	public delegate T FailureAction<out T>(TError e);

	public T Match<T>(SuccessAction<T> success, FailureAction<T> failure) =>
		IsOk
			? success(_value!)
			: failure(_error!);

	public void MatchDo(Action<TValue> success, Action<TError> failure)
	{
		if (IsOk)
		{
			success(_value!);
		}
		else
		{
			failure(_error!);
		}
	}

	public bool IsOk { get; }

	public Result<T, TError> Map<T>(Func<TValue, T> f) =>
		IsOk ? new Result<T, TError>(f(_value!)) : new Result<T, TError>(_error!);

	public Result<TValue, TE> MapError<TE>(Func<TError, TE> f) =>
		IsOk ? new Result<TValue, TE>(_value!) : new Result<TValue, TE>(f(_error!));

	public Result<T, TError> Then<T>(Func<TValue, Result<T, TError>> f) =>
		IsOk ? f(_value!) : new Result<T, TError>(_error!);

	public TValue Value =>
		IsOk ? _value! : throw new InvalidOperationException("Failed result don't have value.");

	public TError Error =>
		!IsOk ? _error! : throw new InvalidOperationException("Successful result don't have error.");

	public static Result<T, Exception> Catch<T>(Func<T> func)
	{
		try
		{
			return func();
		}
		catch (Exception e)
		{
			return Result<T, Exception>.Fail(e);
		}
	}

	public TValue? AsNullable() => IsOk ? _value : default;
}

public record Result<TError> : Result<Unit, TError>
{
	private Result(Unit value) : base(value)
	{
	}

	private Result(TError error) : base(error)
	{
	}

	public static implicit operator Result<TError>(TError error) => new(error);
	public static Result<TError> Ok() => new(Unit.Instance);
	public static new Result<TError> Fail(TError error) => new(error);

	public Result<T> ThenError<T>(Func<TError, T> f) =>
		IsOk ? Result<T>.Ok() : Result<T>.Fail(f(Error));
}

public static class ResultExtensions
{
	public static Result<TValue[], TError[]> SequenceResults<TValue, TError>(this IEnumerable<Result<TValue, TError>> results)
	{
		var values = new List<TValue>();
		var errors = new List<TError>();

		foreach (var r in results)
		{
			if (r.IsOk)
			{
				if (errors.Count == 0)
				{
					values.Add(r.Value);
				}
			}
			else
			{
				errors.Add(r.Error);
			}
		}

		if (errors.Count > 0)
		{
			return Result<TValue[], TError[]>.Fail(errors.ToArray());
		}

		return Result<TValue[], TError[]>.Ok(values.ToArray());
	}

	public static Result<TError[]> SequenceResults<TError>(this IEnumerable<Result<TError>> results)
	{
		var errors = new List<TError>();
		foreach (var r in results)
		{
			if (!r.IsOk)
			{
				errors.Add(r.Error);
			}
		}

		if (errors.Count > 0)
		{
			return Result<TError[]>.Fail(errors.ToArray());
		}

		return Result<TError[]>.Ok();
	}
}
