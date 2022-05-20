namespace WalletWasabi.Fluent.Generators;

internal class AutoNotifySyntaxReceiver : FieldsWithAttributeSyntaxReceiver
{
	public override string AttributeClass => AutoNotifyGenerator.AutoNotifyAttributeDisplayString;
}