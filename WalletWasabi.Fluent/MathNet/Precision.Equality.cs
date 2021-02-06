// <copyright file="Precision.Equality.cs" company="Math.NET">
// Math.NET Numerics, part of the Math.NET Project
// http://numerics.mathdotnet.com
// http://github.com/mathnet/mathnet-numerics
//
// Copyright (c) 2009-2013 Math.NET
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// </copyright>

using System;

namespace MathNet.Numerics
{
	public static partial class Precision
	{
		/// <summary>
		/// Compares two doubles and determines if they are equal
		/// within the specified maximum absolute error.
		/// </summary>
		/// <param name="a">The norm of the first value (can be negative).</param>
		/// <param name="b">The norm of the second value (can be negative).</param>
		/// <param name="diff">The norm of the difference of the two values (can be negative).</param>
		/// <param name="maximumAbsoluteError">The absolute accuracy required for being almost equal.</param>
		/// <returns>True if both doubles are almost equal up to the specified maximum absolute error, false otherwise.</returns>
		public static bool AlmostEqualNorm(this double a, double b, double diff, double maximumAbsoluteError)
		{
			// If A or B are infinity (positive or negative) then
			// only return true if they are exactly equal to each other -
			// that is, if they are both infinities of the same sign.
			if (double.IsInfinity(a) || double.IsInfinity(b))
			{
				return a == b;
			}

			// If A or B are a NAN, return false. NANs are equal to nothing,
			// not even themselves.
			if (double.IsNaN(a) || double.IsNaN(b))
			{
				return false;
			}

			return Math.Abs(diff) < maximumAbsoluteError;
		}

		/// <summary>
		/// Checks whether two real numbers are almost equal.
		/// </summary>
		/// <param name="a">The first number</param>
		/// <param name="b">The second number</param>
		/// <returns>true if the two values differ by no more than 10 * 2^(-52); false otherwise.</returns>
		public static bool AlmostEqual(this double a, double b)
		{
			return AlmostEqualNorm(a, b, a - b, DefaultDoubleAccuracy);
		}
	}
}