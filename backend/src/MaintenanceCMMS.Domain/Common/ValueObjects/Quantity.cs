namespace MaintenanceCMMS.Domain.Common.ValueObjects;

public readonly record struct Quantity
{
    public Quantity(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public static Quantity Zero => new(0);

    public override string ToString() => Value.ToString("0.####");
}

