using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace WebApiRest.Validation;

/// <summary>
/// Rejects a collection that holds a null item, such as the JSON body <c>[null]</c>. Model validation checks
/// the properties of each item but skips null items, so without this they would reach the action.
/// A failure is answered like any other invalid body: 400 with the VALIDATION code.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NoNullItemsAttribute : ValidationAttribute
{
    public NoNullItemsAttribute()
        : base("Every item of {0} is required.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not IEnumerable items || items.Cast<object?>().All(item => item is not null);
}
