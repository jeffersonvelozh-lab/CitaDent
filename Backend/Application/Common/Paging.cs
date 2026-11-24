using Domain.Common;

namespace Application.Common;

/// <summary>Limits shared by every paged query.</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Throws VALIDATION when the page is below 1 or the size is outside 1..<see cref="MaxPageSize"/>.</summary>
    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new DomainException(DomainException.ValidationCode, "The page must be 1 or greater.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new DomainException(
                DomainException.ValidationCode, $"The page size must be between 1 and {MaxPageSize}.");
        }
    }
}
