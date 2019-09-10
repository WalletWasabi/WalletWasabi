using Newtonsoft.Json;
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
			private bool BooleanProperty { get; set; }

			[ValidateMethod(nameof(ValidateProperty))]
			private string StringProperty { get; set; }
		}

		[Fact]
		public void PropertiesWithValidationTest()
		{
			var testClass = new ValidateTestClass();

			var validator = Validator.PropertiesWithValidation(testClass);
			Assert.Equal(2, validator.Count());
		}

		[Fact]
		public void ErrorDescriptorsTest()
		{
			ErrorDescriptors eds = new ErrorDescriptors()
			{
				new ErrorDescriptor(ErrorSeverity.Default,"My default error descriptor"),
				new ErrorDescriptor(ErrorSeverity.Info,"My info error descriptor"),
				new ErrorDescriptor(ErrorSeverity.Warning,"My warning error descriptor"),
				new ErrorDescriptor(ErrorSeverity.Error,"My error error descriptor")
			};

			// Constructor tests

			Assert.Equal("My info error descriptor", eds[1].Message);
			Assert.Equal(ErrorSeverity.Info, eds[1].Severity);

			// Serialize and de-serialize test

			var serialized = JsonConvert.SerializeObject(eds);

			var converter = new ErrorDescriptorsJsonConverter();

			var deserialized = (ErrorDescriptors)converter.Convert(new[] { new Exception(serialized) }, eds.GetType(), null, CultureInfo.InvariantCulture);

			Assert.Equal(4, deserialized.Count());

			for (int i = 0; i < eds.Count; i++)
			{
				Assert.Equal(eds[i], deserialized[i]);
			}
		}
	}
}
