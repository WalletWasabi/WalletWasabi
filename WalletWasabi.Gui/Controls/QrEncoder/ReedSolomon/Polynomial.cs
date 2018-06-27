using System;

namespace Gma.QrCodeNet.Encoding.ReedSolomon
{
	internal sealed class Polynomial
	{
		
		private readonly int[] m_Coefficients;
		
		internal int[] Coefficients
		{
			get
			{
				return m_Coefficients;
			}
		}
		
		private readonly GaloisField256 m_GField;
		
		internal GaloisField256 GField
		{
			get
			{
				return m_GField;
			}
		}
		
		
		
		internal int Degree
		{
			get
			{
				return Coefficients.Length - 1;
			}
		}
		
		private readonly int m_primitive;
		
		internal int Primitive
		{
			get
			{
				return m_primitive;
			}
		}
		
		internal bool isMonomialZero
		{
			get
			{
				return m_Coefficients[0] == 0;
			}
		}
		
		internal Polynomial(GaloisField256 gfield, int[] coefficients)
		{
			int coefficientsLength = coefficients.Length;
			
			if(coefficientsLength == 0 || coefficients == null)
				throw new ArithmeticException("Can not create empty Polynomial");
			
			m_GField = gfield;
			
			m_primitive = gfield.Primitive;
			
			if(coefficientsLength > 1 && coefficients[0] == 0)
			{
				int firstNonZeroIndex = 1;
				while( firstNonZeroIndex < coefficientsLength && coefficients[firstNonZeroIndex] == 0)
				{
					firstNonZeroIndex++;
				}
				
				if(firstNonZeroIndex == coefficientsLength)
					m_Coefficients = new int[]{0};
				else
				{
					int newLength = coefficientsLength - firstNonZeroIndex;
					m_Coefficients = new int[newLength];
					Array.Copy(coefficients, firstNonZeroIndex, m_Coefficients, 0, newLength);
				}
			}
			else
			{
				m_Coefficients = new int[coefficientsLength];
				Array.Copy(coefficients, m_Coefficients, coefficientsLength);
			}
		}
		
		/// <returns>
		/// coefficient position. where (coefficient)x^degree
		/// </returns>
		internal int GetCoefficient(int degree)
		{
			//eg: x^2 + x + 1. degree 1, reverse position = degree + 1 = 2. 
			//Pos = 3 - 2 = 1
			return m_Coefficients[m_Coefficients.Length - (degree + 1)];
		}
		
		/// <summary>
		/// Add another Polynomial to current one
		/// </summary>
		/// <param name="other">The polynomial need to add or subtract to current one</param>
		/// <returns>Result polynomial after add or subtract</returns>
		internal Polynomial AddOrSubtract(Polynomial other)
		{
			if(this.Primitive != other.Primitive)
			{
				throw new ArgumentException("Polynomial can not perform AddOrSubtract as they don't have same Primitive for GaloisField256");
			}
			if(this.isMonomialZero)
				return other;
			else if(other.isMonomialZero)
				return this;
			
			int otherLength = other.Coefficients.Length;
			int thisLength = this.Coefficients.Length;
			
			if(otherLength > thisLength)
				return CoefficientXor(this.Coefficients, other.Coefficients);
			else
				return CoefficientXor(other.Coefficients, this.Coefficients);
			
		}
		
		
		internal Polynomial CoefficientXor(int[] smallerCoefficients, int[] largerCoefficients)
		{
			if(smallerCoefficients.Length > largerCoefficients.Length)
				throw new ArgumentException("Can not perform CoefficientXor method as smaller Coefficients length is larger than larger one.");
			int targetLength = largerCoefficients.Length;
			int[] xorCoefficient = new int[targetLength];
			int lengthDiff = largerCoefficients.Length - smallerCoefficients.Length;
			
			Array.Copy(largerCoefficients, 0, xorCoefficient, 0, lengthDiff);
			
			for(int index = lengthDiff; index < targetLength; index++)
			{
				xorCoefficient[index] = this.GField.Addition(largerCoefficients[index], smallerCoefficients[index - lengthDiff]);
			}
			
			return new Polynomial(this.GField, xorCoefficient);
		}
		
		/// <summary>
		/// Multiply current Polynomial to anotherone. 
		/// </summary>
		/// <returns>Result polynomial after multiply</returns>
		internal Polynomial Multiply(Polynomial other)
		{
			if(this.Primitive != other.Primitive)
			{
				throw new ArgumentException("Polynomial can not perform Multiply as they don't have same Primitive for GaloisField256");
			}
			if(this.isMonomialZero || other.isMonomialZero)
				return new Polynomial(this.GField, new int[]{0});
			
			int[] aCoefficients = this.Coefficients;
			int aLength = aCoefficients.Length;
			int[] bCoefficient = other.Coefficients;
			int bLength = bCoefficient.Length;
			int[] rCoefficients = new int[aLength + bLength - 1];
			
			for(int aIndex = 0; aIndex < aLength; aIndex++)
			{
				int aCoeff = aCoefficients[aIndex];
				for(int bIndex = 0; bIndex < bLength; bIndex++)
				{
					rCoefficients[aIndex + bIndex] = 
						this.GField.Addition(rCoefficients[aIndex + bIndex], this.GField.Product(aCoeff, bCoefficient[bIndex]));
				}
			}
			return new Polynomial(this.GField, rCoefficients);
			
		}
		
		/// <summary>
		/// Multiplay scalar to current polynomial
		/// </summary>
		/// <returns>result of polynomial after multiply scalar</returns>
		internal Polynomial MultiplyScalar(int scalar)
		{
			if(scalar == 0)
			{
				return new Polynomial(this.GField, new int[]{0});
			}
			else if(scalar == 1)
			{
				return this;
			}
			
			int length = this.Coefficients.Length;
			int[] rCoefficient = new int[length];
			
			for(int index = 0; index < length; index++)
			{
				rCoefficient[index] = this.GField.Product(this.Coefficients[index], scalar);
			}
			
			return new Polynomial(this.GField, rCoefficient);
		}
		
		/// <summary>
		/// divide current polynomial by "other"
		/// </summary>
		/// <returns>Result polynomial after divide</returns>
		internal PolyDivideStruct Divide(Polynomial other)
		{
			if(this.Primitive != other.Primitive)
			{
				throw new ArgumentException("Polynomial can not perform Devide as they don't have same Primitive for GaloisField256");
			}
			if(other.isMonomialZero)
			{
				throw new ArgumentException("Can not devide by Polynomial Zero");
			}
			//this devide by other = a devide by b
			int aLength = this.Coefficients.Length;
			//We will make change to aCoefficient. It will return as remainder
			int[] aCoefficients = new int[aLength];
			Array.Copy(this.Coefficients, 0, aCoefficients, 0, aLength);
			
			
			int bLength = other.Coefficients.Length;
			
			if(aLength < bLength)
				return new PolyDivideStruct(new Polynomial(this.GField, new int[]{0}), this);
			else
			{
				//quotient coefficients
				//qLastIndex = alength - blength  qlength = qLastIndex + 1
				int[] qCoefficients = new int[( aLength - bLength ) + 1];
				
				//Denominator
				int otherLeadingTerm = other.GetCoefficient(other.Degree);
				int inverseOtherLeadingTerm = this.GField.inverse(otherLeadingTerm);
				
				for(int aIndex = 0; aIndex <= aLength - bLength; aIndex++)
				{
					if(aCoefficients[aIndex] != 0)
					{
						int aScalar = this.GField.Product(inverseOtherLeadingTerm, aCoefficients[aIndex]);
						Polynomial term = other.MultiplyScalar(aScalar);
						qCoefficients[aIndex] = aScalar;
					
						int[] bCoefficient = term.Coefficients;
						if(bCoefficient[0] != 0)
						{
							for(int bIndex = 0; bIndex < bLength; bIndex++)
							{
								aCoefficients[aIndex + bIndex] = this.GField.Subtraction(aCoefficients[aIndex + bIndex], bCoefficient[bIndex]);
							}
						}
					}
				}
				
				return new PolyDivideStruct(new Polynomial(this.GField, qCoefficients),
				                            new Polynomial(this.GField, aCoefficients));
			}
			
			
		}
		
		
	}
}
