using Domain.Patients;
using Xunit;

namespace Domain.Tests.Patients;

public class EcuadorianIdTests
{
    [Theory]
    [InlineData("1710034065")]
    [InlineData("0926687856")]
    public void IsValidCedula_ReturnsTrue_ForValidNumbers(string value)
    {
        Assert.True(EcuadorianId.IsValidCedula(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("171003406")]   // 9 digits
    [InlineData("17100340655")] // 11 digits
    [InlineData("17100340AB")]  // not only digits
    [InlineData("1710034066")]  // wrong check digit
    [InlineData("9910034065")]  // invalid province
    [InlineData("1760034065")]  // third digit is 6 or more
    public void IsValidCedula_ReturnsFalse_ForInvalidNumbers(string? value)
    {
        Assert.False(EcuadorianId.IsValidCedula(value));
    }
}