using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class ValidateMethodTests
	{
		public class ValidateTestClass
		{
			public ErrorDescriptors ValidateProperty()
			{
				return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Warning, "My warning error descriptor"), new ErrorDescriptor(ErrorSeverity.Error, "My error error descriptor"));
			}

			[ValidateMethod(nameof(ValidateProperty))]
			public bool BooleanProperty { get; set; }

			[ValidateMethod(nameof(ValidateProperty))]
			public string StringProperty { get; set; }
		}

		[Fact]
		public void PropertiesWithValidationTest()
		{
			var testClass = new ValidateTestClass();

			var validator = Validator.PropertiesWithValidation(testClass);
			Assert.Equal(2, validator.Count());
		}
	}
}
