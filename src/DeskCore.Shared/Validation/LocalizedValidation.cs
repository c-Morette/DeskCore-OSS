using System.ComponentModel.DataAnnotations;
using DeskCore.Shared.Localization;

namespace DeskCore.Shared.Validation;

/// <summary>
/// Atributos de validação com mensagem bilíngue (PT/EN) por pares inline. Funcionam
/// tanto no client (EditForm/DataAnnotationsValidator) quanto no server (model binding),
/// pois a mensagem é resolvida em <see cref="ValidationAttribute.FormatErrorMessage"/>
/// usando <see cref="Msg"/> (cultura atual).
/// </summary>
public sealed class LocRequiredAttribute(string pt, string en) : RequiredAttribute
{
    public override string FormatErrorMessage(string name) => Msg.T(pt, en);
}

public sealed class LocStringLengthAttribute(int maximumLength, string pt, string en) : StringLengthAttribute(maximumLength)
{
    public override string FormatErrorMessage(string name) => Msg.T(pt, en);
}

public sealed class LocEmailAddressAttribute(string pt, string en) : ValidationAttribute
{
    private static readonly EmailAddressAttribute Inner = new();
    public override bool IsValid(object? value) => Inner.IsValid(value);
    public override string FormatErrorMessage(string name) => Msg.T(pt, en);
}

public sealed class LocRangeAttribute(double minimum, double maximum, string pt, string en) : RangeAttribute(minimum, maximum)
{
    public override string FormatErrorMessage(string name) => Msg.T(pt, en);
}
