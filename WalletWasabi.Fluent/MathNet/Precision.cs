// <copyright file="Precision.cs" company="Math.NET">
// Math.NET Numerics, part of the Math.NET Project
// http://numerics.mathdotnet.com
// http://github.com/mathnet/mathnet-numerics
//
// Copyright (c) 2009-2015 Math.NET
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
	/// <summary>
	/// Utilities for working with floating point numbers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Useful links:
	/// <list type="bullet">
	/// <item>
	/// http://docs.sun.com/source/806-3568/ncg_goldberg.html#689 - What every computer scientist should know about floating-point arithmetic
	/// </item>
	/// <item>
	/// http://en.wikipedia.org/wiki/Machine_epsilon - Gives the definition of machine epsilon
	/// </item>
	/// </list>
	/// </para>
	/// </remarks>
	public static partial class Precision
	{
		/// <summary>
		/// The number of binary digits used to represent the binary number for a double precision floating
		/// point value. i.e. there are this many digits used to represent the
		/// actual number, where in a number as: 0.134556 * 10^5 the digits are 0.134556 and the exponent is 5.
		/// </summary>
		private const int DoubleWidth = 53;

		/// <summary>
		/// Standard epsilon, the maximum relative precision of IEEE 754 double-precision floating numbers (64 bit).
		/// According to the definition of Prof. Demmel and used in LAPACK and Scilab.
		/// </summary>
		public static readonly double DoublePrecision = Math.Pow(2, -DoubleWidth);

		/// <summary>
		/// Value representing 10 * 2^(-53) = 1.11022302462516E-15
		/// </summary>
		private static readonly double DefaultDoubleAccuracy = DoublePrecision * 10;
	}
}