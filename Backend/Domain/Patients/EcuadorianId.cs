namespace Domain.Patients;

/// <summary>
/// Validates the Ecuadorian cédula: 10 digits, a valid province code (01 to 24, or 30),
/// a third digit below 6 and a correct check digit (modulo 10 algorithm).
/// </summary>
public static class EcuadorianId
{
    private static readonly int[] Coefficients = [2, 1, 2, 1, 2, 1, 2, 1, 2];

    public static bool IsValidCedula(string? value)
    {
        if (value is null || value.Length != 10 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var province = int.Parse(value[..2]);

        if (province is (< 1 or > 24) && province != 30)
        {
            return false;
        }

        if (value[2] - '0' >= 6)
        {
            return false;
        }

        var sum = 0;

        for (var i = 0; i < 9; i++)
        {
            var product = (value[i] - '0') * Coefficients[i];
            sum += product >= 10 ? product - 9 : product;
        }

        var checkDigit = (10 - (sum % 10)) % 10;

        return checkDigit == value[9] - '0';
    }
}