using System;
using System.Collections.Generic;
using System.Text;

namespace System
{
	public class TypeArgumentException : ArgumentException
	{
		public TypeArgumentException(object value, Type expected, string paramName) : base($"Invalid type: {value.GetType().ToString()}. Expected: {expected.ToString()}.", paramName)
		{
		}
	}
}
