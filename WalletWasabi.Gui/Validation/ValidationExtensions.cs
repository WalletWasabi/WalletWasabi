using System;
using System.Linq.Expressions;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Validation
{
	public delegate void ValidateMethod(IValidationErrors errors);

	public static class ValidationExtensions
	{
		public static void ValidateProperty<TSender, TRet>(this TSender viewModel, Expression<Func<TSender, TRet>> property, ValidateMethod validateMethod) where TSender : IRegisterValidationMethod
		{
			var expression = (MemberExpression)property.Body;

			viewModel.RegisterValidationMethod(expression.Member.Name, validateMethod);
		}
	}
}
