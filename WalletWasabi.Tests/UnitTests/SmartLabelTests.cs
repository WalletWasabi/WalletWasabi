using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.BlockchainAnalysis;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class SmartLabelTests
	{
		[Fact]
		public void LabelParsingTests()
		{
			var label = new SmartLabel();
			Assert.Equal("", label.ToString());

			label = new SmartLabel("");
			Assert.Equal("", label.ToString());

			label = new SmartLabel(null);
			Assert.Equal("", label.ToString());

			label = new SmartLabel(null, null);
			Assert.Equal("", label.ToString());

			label = new SmartLabel(" ");
			Assert.Equal("", label.ToString());

			label = new SmartLabel(",");
			Assert.Equal("", label.ToString());

			label = new SmartLabel(":");
			Assert.Equal("", label.ToString());

			label = new SmartLabel("foo");
			Assert.Equal("foo", label.ToString());

			label = new SmartLabel("foo", "bar");
			Assert.Equal("bar, foo", label.ToString());

			label = new SmartLabel("foo bar");
			Assert.Equal("foo bar", label.ToString());

			label = new SmartLabel("foo bar", "Buz quX@");
			Assert.Equal("Buz quX@, foo bar", label.ToString());

			label = new SmartLabel(new List<string>() { "foo", "bar" });
			Assert.Equal("bar, foo", label.ToString());

			label = new SmartLabel("  foo    ");
			Assert.Equal("foo", label.ToString());

			label = new SmartLabel("foo      ", "      bar");
			Assert.Equal("bar, foo", label.ToString());

			label = new SmartLabel(new List<string>() { "   foo   ", "   bar    " });
			Assert.Equal("bar, foo", label.ToString());

			label = new SmartLabel(new List<string>() { "foo:", ":bar", null, ":buz:", ",", ": , :", "qux:quux", "corge,grault", "", "  ", " , garply, waldo,", " : ,  :  ,  fred  , : , :   plugh, : , : ," });
			Assert.Equal("bar, buz, corge, foo, fred, garply, grault, plugh, quux, qux, waldo", label.ToString());

			label = new SmartLabel(",: foo::bar :buz:,: , :qux:quux, corge,grault  , garply, waldo, : ,  :  ,  fred  , : , :   plugh, : , : ,");
			Assert.Equal("bar, buz, corge, foo, fred, garply, grault, plugh, quux, qux, waldo", label.ToString());
		}

		[Fact]
		public void LabelEqualityTests()
		{
			var label = new SmartLabel("foo");
			var label2 = new SmartLabel(label.ToString());
			Assert.Equal(label, label2);

			label = new SmartLabel("foo, bar, buz");
			label2 = new SmartLabel(label.ToString());
			Assert.Equal(label, label2);

			label2 = new SmartLabel("bar, buz, foo");
			Assert.Equal(label, label2);

			var label3 = new SmartLabel("bar, buz");
			Assert.NotEqual(label, label3);

			SmartLabel label4 = null;
			SmartLabel label5 = null;
			Assert.Equal(label4, label5);
			Assert.NotEqual(label, label4);
			Assert.False(label.Equals(label4));
			Assert.False(label == label4);
		}

		[Fact]
		public void SpecialLabelTests()
		{
			var label = new SmartLabel("");
			Assert.Equal(label, SmartLabel.Empty);
			Assert.True(label.IsEmpty);

			label = new SmartLabel("foo, bar, buz");
			var label2 = new SmartLabel("qux");

			label = SmartLabel.Merge(label, label2);
			Assert.Equal("bar, buz, foo, qux", label.ToString());

			label2 = new SmartLabel("qux", "bar");
			label = SmartLabel.Merge(label, label2);
			Assert.Equal(4, label.Labels.Count());
			Assert.Equal("bar, buz, foo, qux", label.ToString());

			label2 = new SmartLabel("Qux", "Bar");
			label = SmartLabel.Merge(label, label2, null);
			Assert.Equal(4, label.Labels.Count());
			Assert.Equal("bar, buz, foo, qux", label.ToString());
		}
	}
}
