namespace MaintenanceCMMS.Domain.Common.ValueObjects;

public readonly record struct EntityCode
{
    public EntityCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Code is required.", nameof(value));
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

