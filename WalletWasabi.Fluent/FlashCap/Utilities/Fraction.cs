////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;

namespace FlashCap.Utilities;

/// <summary>
/// Fraction number structure.
/// </summary>
#if !NETSTANDARD1_3
[Serializable]
#endif
[DebuggerDisplay("{PrettyPrint}")]
public readonly struct Fraction :
    IEquatable<Fraction>, IComparable<Fraction>
{
    /// <summary>
    /// The numerator.
    /// </summary>
    public readonly int Numerator;

    /// <summary>
    /// The denominator.
    /// </summary>
    public readonly int Denominator;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="numerator">The numerator</param>
    /// <param name="denominator">The denominator</param>
    /// <exception cref="DivideByZeroException">Denominator is zero and real number is not zero.</exception>
    public Fraction(int numerator, int denominator)
    {
        if (numerator == 0)
        {
            this.Numerator = 0;
            this.Denominator = 0;
        }
        else if (denominator != 0)
        {
            this.Numerator = numerator;
            this.Denominator = denominator;
        }
        else
        {
            throw new DivideByZeroException();
        }
    }

    /// <summary>
    /// Construction method.
    /// </summary>
    /// <param name="numerator">The numerator</param>
    /// <param name="denominator">The denominator</param>
    /// <exception cref="DivideByZeroException">Denominator is zero and real number is not zero.</exception>
    /// <returns>Fraction</returns>
    public static Fraction Create(int numerator, int denominator) =>
        new(numerator, denominator);
    
    /// <summary>
    /// Construction method.
    /// </summary>
    /// <param name="value">The numerator (and the denominator is 1)</param>
    /// <returns>Fraction</returns>
    public static Fraction Create(int value) =>
        new(value, 1);
    
    private static long gcm(long a, long b) =>
        b == 0 ?
            0 :
            a % b is { } mod && mod == 0 ?
                b : gcm(b, mod);

    private static long lcm(long a, long b) =>
        b == 0 ?
            0 :
            a * b / gcm(a, b);

    private static Fraction Reduce(long n, long d)
    {
        var g = gcm(n, d);
        return g switch
        {
            0 => Zero,
            1 => new Fraction((int)n, (int)d),
            _ => new Fraction((int)(n / g), (int)(d / g)),
        };
    }

    /// <summary>
    /// Reduce this fraction.
    /// </summary>
    /// <returns>Reduced fraction</returns>
    public Fraction Reduce() =>
        Reduce(this.Numerator, this.Denominator);

    /// <summary>
    /// Get reciprocal fraction.
    /// </summary>
    /// <returns>Reciprocal fraction</returns>
    public Fraction Reciprocal() =>
        new(this.Denominator, this.Numerator);

    private void Prepare(in Fraction rhs,
        out long pd, out long tn, out long rn)
    {
        pd = lcm(this.Denominator, rhs.Denominator);
        var tm = pd / this.Denominator;
        var rm = pd / rhs.Denominator;
        tn = this.Numerator * tm;
        rn = rhs.Numerator * rm;
    }

    /// <summary>
    /// Add fraction number.
    /// </summary>
    /// <param name="rhs">Fraction</param>
    /// <param name="reduce">Perform reducing fraction when calculated</param>
    /// <returns>Calculated fraction</returns>
    public Fraction Add(Fraction rhs, bool reduce = true)
    {
        if (this.Denominator == 0)
        {
            Debug.Assert(this.Numerator == 0);
            return rhs;
        }
        if (rhs.Denominator == 0)
        {
            Debug.Assert(rhs.Numerator == 0);
            return rhs;
        }
        this.Prepare(in rhs, out var pd, out var tn, out var rn);
        var pn = tn + rn;
        return reduce ? Reduce(pn, pd) : new Fraction((int)pn, (int)pd);
    }

    /// <summary>
    /// Multiple fraction number.
    /// </summary>
    /// <param name="rhs">Fraction</param>
    /// <param name="reduce">Perform reducing fraction when calculated</param>
    /// <returns>Calculated fraction</returns>
    public Fraction Mult(Fraction rhs, bool reduce = true)
    {
        var pn = (long)this.Numerator * rhs.Numerator;
        var pd = (long)this.Denominator * rhs.Denominator;
        return reduce ? Reduce(pn, pd) : new Fraction((int)pn, (int)pd);
    }

    /// <summary>
    /// Modulo fraction number.
    /// </summary>
    /// <param name="rhs">Fraction</param>
    /// <param name="reduce">Perform reducing fraction when calculated</param>
    /// <returns>Calculated fraction</returns>
    public Fraction Mod(Fraction rhs, bool reduce = true)
    {
        if (this.Denominator == 0)
        {
            Debug.Assert(this.Numerator == 0);
            return rhs;
        }
        if (rhs.Denominator == 0)
        {
            Debug.Assert(rhs.Numerator == 0);
            return rhs;
        }
        this.Prepare(in rhs, out var pd, out var tn, out var rn);
        var m = tn % rn;
        return reduce ? Reduce(m, pd) : new Fraction((int)m, (int)pd);
    }

    /// <summary>
    /// Get hash code.
    /// </summary>
    /// <returns>Hash code</returns>
    public override int GetHashCode() =>
        this.Numerator ^ this.Denominator;

    private static bool ExactEquals(Fraction lhs, Fraction rhs) =>
        lhs.Numerator == rhs.Numerator &&
        lhs.Denominator == rhs.Denominator;

    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True if equals</returns>
    public bool Equals(Fraction rhs) =>
        ExactEquals(this, rhs) ||
        ExactEquals(this.Reduce(), rhs.Reduce());

    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="obj">Fraction number</param>
    /// <returns>True if equals</returns>
    public override bool Equals(object? obj) =>
        obj is Fraction rhs && this.Equals(rhs);

    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True if equals</returns>
    bool IEquatable<Fraction>.Equals(Fraction rhs) =>
        this.Equals(rhs);

    /// <summary>
    /// Get compared index.
    /// </summary>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Compared relative index</returns>
    public int CompareTo(Fraction rhs)
    {
        if (this.Denominator == 0)
        {
            Debug.Assert(this.Numerator == 0);
            return this.Numerator.CompareTo(rhs.Numerator);
        }
        if (rhs.Denominator == 0)
        {
            Debug.Assert(rhs.Numerator == 0);
            return this.Numerator.CompareTo(rhs.Numerator);
        }
        var pd = lcm(this.Denominator, rhs.Denominator);
        var tm = pd / this.Denominator;
        var rm = pd / rhs.Denominator;
        var tn = this.Numerator * tm;
        var rn = rhs.Numerator * rm;
        return tn.CompareTo(rn);
    }

    /// <summary>
    /// Get compared index.
    /// </summary>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Compared relative index</returns>
    int IComparable<Fraction>.CompareTo(Fraction rhs) =>
        this.CompareTo(rhs);

    /// <summary>
    /// Pretty printer.
    /// </summary>
    /// <returns>String</returns>
    public string PrettyPrint =>
        this.Denominator == 0 ?
            "0 [0.0]" :
            this.Reduce() is { } reduced && ExactEquals(reduced, this) ?
                $"{this.Numerator}/{this.Denominator} [{(double)this.Numerator / this.Denominator:F3}]" :
                $"{this.Numerator}/{this.Denominator} [{reduced}, {(double)this.Numerator / this.Denominator:F3}]";

    /// <summary>
    /// Get string value.
    /// </summary>
    /// <returns>String</returns>
    public override string ToString() =>
        $"{this.Numerator}/{this.Denominator}";

    /// <summary>
    /// Zero instance of fraction.
    /// </summary>
    public static readonly Fraction Zero =
        new(0, 0);

    /// <summary>
    /// Add fraction number.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Fraction</returns>
    public static Fraction operator +(Fraction lhs, Fraction rhs) =>
        lhs.Add(rhs);
    /// <summary>
    /// Subtract fraction number.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Fraction</returns>
    public static Fraction operator -(Fraction lhs, Fraction rhs) =>
        lhs.Add(new Fraction(-rhs.Numerator, rhs.Denominator));
    /// <summary>
    /// Multiple fraction number.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Fraction</returns>
    public static Fraction operator *(Fraction lhs, Fraction rhs) =>
        lhs.Mult(rhs);
    /// <summary>
    /// Divide fraction number.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Fraction</returns>
    public static Fraction operator /(Fraction lhs, Fraction rhs) =>
        lhs.Mult(rhs.Reciprocal());
    /// <summary>
    /// Modulo fraction number.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>Fraction</returns>
    public static Fraction operator %(Fraction lhs, Fraction rhs) =>
        lhs.Mod(rhs);

    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True if equals</returns>
    public static bool operator ==(Fraction lhs, Fraction rhs) =>
        lhs.Equals(rhs);
    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True if not equals</returns>
    public static bool operator !=(Fraction lhs, Fraction rhs) =>
        !lhs.Equals(rhs);
    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True lesser than rhs</returns>
    public static bool operator <(Fraction lhs, Fraction rhs) =>
        lhs.CompareTo(rhs) < 0;
    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True lesser than or equal rhs</returns>
    public static bool operator <=(Fraction lhs, Fraction rhs) =>
        lhs.CompareTo(rhs) <= 0;
    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True greater than rhs</returns>
    public static bool operator >(Fraction lhs, Fraction rhs) =>
        lhs.CompareTo(rhs) > 0;
    /// <summary>
    /// Compare equality.
    /// </summary>
    /// <param name="lhs">Fraction number</param>
    /// <param name="rhs">Fraction number</param>
    /// <returns>True greater than or equal rhs</returns>
    public static bool operator >=(Fraction lhs, Fraction rhs) =>
        lhs.CompareTo(rhs) >= 0;

    /// <summary>
    /// Implicitly conversion from integer.
    /// </summary>
    /// <param name="numerator">Numerator value</param>
    public static implicit operator Fraction(int numerator) =>
        new(numerator, 1);
    /// <summary>
    /// Implicitly conversion to floating point value.
    /// </summary>
    /// <param name="lhs">Fraction</param>
    public static implicit operator double(Fraction lhs) =>
        lhs.Denominator == 0 ?
            0.0 :
            (double)lhs.Numerator / lhs.Denominator;
}
