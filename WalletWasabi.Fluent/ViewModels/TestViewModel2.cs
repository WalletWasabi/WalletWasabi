namespace WalletWasabi.Fluent.ViewModels
{
	// TODO: For testing only.
	public partial class TestViewModel2
	{
		[AutoNotify(PropertyName = "TestName")] private bool _prop1;
		[AutoNotify(Modifier = AccessModifier.None)] private bool _prop2 = false;
		[AutoNotify(Modifier = AccessModifier.Public)] private bool _prop3;
		[AutoNotify(Modifier = AccessModifier.Protected)] private bool _prop4;
		[AutoNotify(Modifier = AccessModifier.Private)] private bool _prop5;
		[AutoNotify(Modifier = AccessModifier.Internal)] private bool _prop6;
	}
}