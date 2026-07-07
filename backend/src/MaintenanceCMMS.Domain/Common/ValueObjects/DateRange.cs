namespace MaintenanceCMMS.Domain.Common.ValueObjects;

public readonly record struct DateRange
{
    public DateRange(DateOnly start, DateOnly? end = null)
    {
        if (end.HasValue && end.Value < start)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly? End { get; }

    public bool Contains(DateOnly date) => date >= Start && (!End.HasValue || date <= End.Value);
}

