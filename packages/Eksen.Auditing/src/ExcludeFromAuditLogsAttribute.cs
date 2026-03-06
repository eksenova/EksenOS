namespace Eksen.Auditing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class ExcludeFromAuditLogsAttribute : Attribute;