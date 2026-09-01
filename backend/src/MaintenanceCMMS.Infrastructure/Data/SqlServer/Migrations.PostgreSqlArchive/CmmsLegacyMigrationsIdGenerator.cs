using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

#pragma warning disable EF1001
internal sealed class CmmsLegacyMigrationsIdGenerator : MigrationsIdGenerator
{
    private const int LegacyTimestampLength = 12;

    public override bool IsValidId(string value)
    {
        return base.IsValidId(value) || IsLegacyId(value);
    }

    public override string GetName(string id)
    {
        return IsLegacyId(id) ? id[(LegacyTimestampLength + 1)..] : base.GetName(id);
    }

    private static bool IsLegacyId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= LegacyTimestampLength + 1 || value[LegacyTimestampLength] != '_')
        {
            return false;
        }

        for (var i = 0; i < LegacyTimestampLength; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
#pragma warning restore EF1001