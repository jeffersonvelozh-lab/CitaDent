using WebApiRest.Errors;

namespace WebApiRest.Tests.Errors;

/// <summary>Unit tests of the code-to-status table: one case per known code.</summary>
public class ErrorStatusCodesTests
{
    [Theory]
    [InlineData("VALIDATION", 400)]
    [InlineData("AUTH-INVALID-CREDENTIALS", 401)]
    [InlineData("RN-01", 422)]
    [InlineData("RN-02", 409)]
    [InlineData("RN-03", 409)]
    [InlineData("RN-04", 422)] // RN-04 only relaxes RN-02/RN-03 and is never thrown; falls to the default.
    [InlineData("RN-05", 409)]
    [InlineData("RN-06", 422)]
    [InlineData("RN-07", 409)]
    [InlineData("RN-08", 422)]
    [InlineData("RN-09", 403)]
    [InlineData("RN-10", 422)]
    [InlineData("RN-11", 409)]
    [InlineData("RN-12", 422)]
    [InlineData("RN-13", 422)]
    [InlineData("RN-14", 422)]
    [InlineData("RN-15", 403)]
    [InlineData("RN-16", 422)]
    [InlineData("RN-17", 422)]
    [InlineData("RN-18", 422)]
    [InlineData("RN-19", 403)]
    public void For_KnownCode_ReturnsItsStatus(string code, int expected)
    {
        Assert.Equal(expected, ErrorStatusCodes.For(code));
    }

    [Theory]
    [InlineData("RN-99")]
    [InlineData("SOMETHING-ELSE")]
    [InlineData("rn-02")] // Codes are case-sensitive.
    public void For_UnknownCode_Returns422(string code)
    {
        Assert.Equal(422, ErrorStatusCodes.For(code));
    }
}
