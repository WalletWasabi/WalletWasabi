namespace WalletWasabi.Fluent.ViewModels
{
	// TODO: For testing only.
	public partial class TestViewModel2
	{
		[AutoNotify(PropertyName = "TestName")] private bool _prop1;
		[AutoNotify(SetterModifier = AccessModifier.None)] private bool _prop2 = false;
		[AutoNotify(SetterModifier = AccessModifier.Public)] private bool _prop3;
		[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _prop4;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _prop5;
		[AutoNotify(SetterModifier = AccessModifier.Internal)] private bool _prop6;
	}
}