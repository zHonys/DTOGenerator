using System;

namespace SynoLib.Generators.Attributes;

/// <summary>
/// Tells the generator that this class should have a DTO
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class HasDTOAttribute : Attribute {
    public string DTOClassName { get; }
    public ConversionForm ConversionForm { get; }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="dtoClassname">
    /// The name of the generated DTO, [class] will be replaced with the class' name;
    /// Default is "[class]DTO"
    /// </param>
    /// <param name="conversionForm"></param>
    public HasDTOAttribute(string dtoClassname="[class]DTO", ConversionForm conversionForm = ConversionForm.Explicit) {
        DTOClassName = dtoClassname;
        ConversionForm = conversionForm;
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
    /// <summary> A reference method will be created to handle conversions. e.g. DTOType dto = model.ToDTO(); </summary>
    ReferenceMethods = 8,
}