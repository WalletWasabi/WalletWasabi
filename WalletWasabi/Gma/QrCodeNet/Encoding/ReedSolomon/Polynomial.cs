using System;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	internal sealed class Polynomial
	{
		internal int[] Coefficients { get; }

		internal GaloisField256 GField { get; }

		internal int Degree => Coefficients.Length - 1;

		internal int Primitive { get; }

		internal bool IsMonomialZero => Coefficients[0] == 0;

		internal Polynomial(GaloisField256 gfield, int[] coefficients)
		{
			int coefficientsLength = coefficients.Length;

			if (coefficientsLength == 0 || coefficients is null)
			{
				throw new ArithmeticException($"Cannot create empty {nameof(Polynomial)}.");
			}

			GField = gfield;

			Primitive = gfield.Primitive;

			if (coefficientsLength > 1 && coefficients[0] == 0)
			{
				int firstNonZeroIndex = 1;
				while (firstNonZeroIndex < coefficientsLength && coefficients[firstNonZeroIndex] == 0)
				{
					firstNonZeroIndex++;
				}

				if (firstNonZeroIndex == coefficientsLength)
				{
					Coefficients = new int[] { 0 };
				}
				else
				{
					int newLength = coefficientsLength - firstNonZeroIndex;
					Coefficients = new int[newLength];
					Array.Copy(coefficients, firstNonZeroIndex, Coefficients, 0, newLength);
				}
			}
			else
			{
				Coefficients = new int[coefficientsLength];
				Array.Copy(coefficients, Coefficients, coefficientsLength);
			}
		}

		/// <returns>
		/// coefficient position. where (coefficient)x^degree
		/// </returns>
		internal int GetCoefficient(int degree)
		{
			//eg: x^2 + x + 1. degree 1, reverse position = degree + 1 = 2.
			//Pos = 3 - 2 = 1
			return Coefficients[Coefficients.Length - (degree + 1)];
		}

		/// <summary>
		/// Add another Polynomial to current one
		/// </summary>
		/// <param name="other">The polynomial need to add or subtract to current one</param>
		/// <returns>Result polynomial after add or subtract</returns>
		internal Polynomial AddOrSubtract(Polynomial other)
		{
			if (Primitive != other.Primitive)
			{
				throw new ArgumentException($"{nameof(Polynomial)} cannot perform {nameof(AddOrSubtract)} as they do not have the same {nameof(Primitive)}" +
					$" for {nameof(GaloisField256)}.");
			}
			if (IsMonomialZero)
			{
				return other;
			}
			else if (other.IsMonomialZero)
			{
				return this;
			}

			int otherLength = other.Coefficients.Length;
			int thisLength = Coefficients.Length;

			if (otherLength > thisLength)
			{
				return CoefficientXor(Coefficients, other.Coefficients);
			}
			else
			{
				return CoefficientXor(other.Coefficients, Coefficients);
			}
		}

		internal Polynomial CoefficientXor(int[] smallerCoefficients, int[] largerCoefficients)
		{
			if (smallerCoefficients.Length > largerCoefficients.Length)
			{
				throw new ArgumentException($"Cannot perform {nameof(CoefficientXor)} method as smaller {nameof(Coefficients)} length is greater than the larger one.");
			}

			int targetLength = largerCoefficients.Length;
			int[] xorCoefficient = new int[targetLength];
			int lengthDiff = largerCoefficients.Length - smallerCoefficients.Length;

			Array.Copy(largerCoefficients, 0, xorCoefficient, 0, lengthDiff);

			for (int index = lengthDiff; index < targetLength; index++)
			{
				xorCoefficient[index] = GField.Addition(largerCoefficients[index], smallerCoefficients[index - lengthDiff]);
			}

			return new Polynomial(GField, xorCoefficient);
		}

		/// <summary>
		/// Multiply current Polynomial to anotherone.
		/// </summary>
		/// <returns>Result polynomial after multiply</returns>
		internal Polynomial Multiply(Polynomial other)
		{
			if (Primitive != other.Primitive)
			{
				throw new ArgumentException($"{nameof(Polynomial)} cannot perform {nameof(Multiply)} as they do not have the same {nameof(Primitive)}" +
					$" for {nameof(GaloisField256)}.");
			}
			if (IsMonomialZero || other.IsMonomialZero)
			{
				return new Polynomial(GField, new int[] { 0 });
			}

			int[] aCoefficients = Coefficients;
			int aLength = aCoefficients.Length;
			int[] bCoefficient = other.Coefficients;
			int bLength = bCoefficient.Length;
			int[] rCoefficients = new int[aLength + bLength - 1];

			for (int aIndex = 0; aIndex < aLength; aIndex++)
			{
				int aCoeff = aCoefficients[aIndex];
				for (int bIndex = 0; bIndex < bLength; bIndex++)
				{
					rCoefficients[aIndex + bIndex] =
						GField.Addition(rCoefficients[aIndex + bIndex], GField.Product(aCoeff, bCoefficient[bIndex]));
				}
			}
			return new Polynomial(GField, rCoefficients);
		}

		/// <summary>
		/// Multiplay scalar to current polynomial
		/// </summary>
		/// <returns>result of polynomial after multiply scalar</returns>
		internal Polynomial MultiplyScalar(int scalar)
		{
			if (scalar == 0)
			{
				return new Polynomial(GField, new int[] { 0 });
			}
			else if (scalar == 1)
			{
				return this;
			}

			int length = Coefficients.Length;
			int[] rCoefficient = new int[length];

			for (int index = 0; index < length; index++)
			{
				rCoefficient[index] = GField.Product(Coefficients[index], scalar);
			}

			return new Polynomial(GField, rCoefficient);
		}

		/// <summary>
		/// divide current polynomial by "other"
		/// </summary>
		/// <returns>Result polynomial after divide</returns>
		internal PolyDivideStruct Divide(Polynomial other)
		{
			if (Primitive != other.Primitive)
			{
				throw new ArgumentException($"{nameof(Polynomial)} cannot perform {nameof(Divide)} as they do not have the same {nameof(Primitive)}" +
					$" for {nameof(GaloisField256)}.");
			}
			if (other.IsMonomialZero)
			{
				throw new ArgumentException($"Cannot divide by {nameof(Polynomial)} Zero.");
			}
			//this divide by other = a divide by b
			int aLength = Coefficients.Length;
			//We will make change to aCoefficient. It will return as remainder
			int[] aCoefficients = new int[aLength];
			Array.Copy(Coefficients, 0, aCoefficients, 0, aLength);

			int bLength = other.Coefficients.Length;

			if (aLength < bLength)
			{
				return new PolyDivideStruct(new Polynomial(GField, new int[] { 0 }), this);
			}
			else
			{
				//quotient coefficients
				//qLastIndex = alength - blength  qlength = qLastIndex + 1
				int[] qCoefficients = new int[(aLength - bLength) + 1];

				//Denominator
				int otherLeadingTerm = other.GetCoefficient(other.Degree);
				int inverseOtherLeadingTerm = GField.Inverse(otherLeadingTerm);

				for (int aIndex = 0; aIndex <= aLength - bLength; aIndex++)
				{
					if (aCoefficients[aIndex] != 0)
					{
						int aScalar = GField.Product(inverseOtherLeadingTerm, aCoefficients[aIndex]);
						Polynomial term = other.MultiplyScalar(aScalar);
						qCoefficients[aIndex] = aScalar;

						int[] bCoefficient = term.Coefficients;
						if (bCoefficient[0] != 0)
						{
							for (int bIndex = 0; bIndex < bLength; bIndex++)
							{
								aCoefficients[aIndex + bIndex] = GField.Subtraction(aCoefficients[aIndex + bIndex], bCoefficient[bIndex]);
							}
						}
					}
				}

				return new PolyDivideStruct(new Polynomial(GField, qCoefficients),
											new Polynomial(GField, aCoefficients));
			}
		}
	}
}
