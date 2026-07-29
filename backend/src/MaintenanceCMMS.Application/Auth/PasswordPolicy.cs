using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Application.Auth;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static void EnsureValid(string? password, int minimumLength = MinimumLength, int maximumLength = MaximumLength)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainException("La nueva contraseña es obligatoria.");
        }

        var unmetRequirements = new List<string>();
        if (password.Length < minimumLength)
        {
            unmetRequirements.Add($"tener al menos {minimumLength} caracteres");
        }

        if (password.Length > maximumLength)
        {
            unmetRequirements.Add($"tener como máximo {maximumLength} caracteres");
        }

        if (!password.Any(char.IsUpper))
        {
            unmetRequirements.Add("incluir una letra mayúscula");
        }

        if (!password.Any(char.IsLower))
        {
            unmetRequirements.Add("incluir una letra minúscula");
        }

        if (!password.Any(char.IsDigit))
        {
            unmetRequirements.Add("incluir un número");
        }

        if (!password.Any(character => !char.IsLetterOrDigit(character)))
        {
            unmetRequirements.Add("incluir un carácter especial");
        }

        if (unmetRequirements.Count > 0)
        {
            throw new DomainException($"La nueva contraseña debe {string.Join(", ", unmetRequirements)}.");
        }
    }
}
