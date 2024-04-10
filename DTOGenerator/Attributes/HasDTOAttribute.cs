using System;

namespace SynoLib.Generators.Attributes;

/// <summary>
/// Tells the generator that this class should have a DTO
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class HasDTOAttribute : Attribute {

    /// <summary>
    /// The name of the generated DTO, [class] will be replaced with the class' name;<br/>
    /// Default is <c>"[class]DTO"</c>.
    /// </summary>
    public string DTOClassName { get; set; } = "[class]DTO";
    /// <summary>
    /// <para>
    /// Specifies how the model can be converted to DTO, and vice versa;<br/>
    /// More than one flag can be used at the same time;<br/>
    /// Explicit has priority over implicit, and the latter will be ignored if both are used;
    /// </para>
    /// <para>
    ///     Default value is <c>Conversion.Explicit</c>.
    /// </para>
    /// </summary>
    public ConversionForm ConversionForm { get; set; } = ConversionForm.Explicit;
    /// <summary>
    /// Tells the generator that this class should have a DTO
    /// </summary>
    public HasDTOAttribute() { }
}

/// <summary>
/// Specifies how the model can be converted to DTO, and vice versa;<br/>
/// More than one flag can be used at the same time.
/// </summary>
public enum ConversionForm {
    /// <summary>
    /// No conversion method will be created;><br/>
    /// Note that None will be ignored if used with any other flag.
    /// </summary>
    None = 0,
    /// <summary>
    /// Model can be converted explicitly.<br/>
    /// <example>
    /// e.g. 
    /// <c>DTOType dto = (DTOType)model;</c>
    /// </example>
    /// </summary>
    Explicit = 1,
    /// <summary> Model and DTO can be converted implicitly.<br/>
    /// <example>
    /// e.g. 
    /// <c>DTOType dto = model;</c>
    /// </example>
    /// </summary>
    Implicit = 2,
    /// <summary> A static method on the DTO class will be created to handle conversions.<br/>
    /// <example>
    /// e.g.
    /// <c>DTOType dto = DTOType.ToDTO(model);</c>
    /// </example>
    /// </summary>
    StaticMethods = 4,
    /// <summary>
    /// A reference method will be created to handle conversions.<br/>
    /// <example>
    /// e.g.
    /// <c>ModelType model = dto.ToModel()</c>
    /// </example><br/>
    /// Note that this only creates a conversion from DTO to Model, and not Model to DTO
    /// </summary>
    ReferenceMethods = 8,
}