using System;

namespace SynoLib.Generators.Attributes;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class HasDTOAttribute : Attribute {

    private ConversionForm _conversionForm { get; }
    public HasDTOAttribute(ConversionForm conversionForm = ConversionForm.Explicit) {
        _conversionForm = conversionForm;
    }
}

/// <summary>
/// Specifies how the model can be converted to DTO, and vice versa
/// More than one flag can be used at the same time.
/// </summary>
public enum ConversionForm {
    /// <summary> No conversion method will be created. Note that None will be ignored if used with any other flag. </summary>
    None = 0,
    /// <summary> Model can be converted explicitly. e.g. DTOType dto = (DTOType)model; </summary>
    Explicit = 1,
    /// <summary> Model and DTO can be converted implicitly. e.g. DTOType dto = model; </summary>
    Implicit = 2,
    /// <summary> A static method on the DTO class will be created to handle conversions. e.g. DTOType dto = DTOType.ToDTO(model); </summary>
    StaticMethods = 4,
    /// <summary> A static static extension method will be created to handle conversions. e.g. DTOType dto = model.ToDTO(); </summary>
    StaticExtensionMethods = 8,
}