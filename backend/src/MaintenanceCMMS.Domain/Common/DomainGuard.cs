namespace MaintenanceCMMS.Domain.Common;

public static class DomainGuard
{
    public static void AgainstEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }
    }

    public static void AgainstNegative(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new DomainException($"{fieldName} cannot be negative.");
        }
    }
}

