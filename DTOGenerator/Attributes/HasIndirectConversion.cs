using SynoLib.Generators.DTOGenerator.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SynoLib.Generators.DTOGenerator.Attributes;

/// <summary>
/// Specifies that this varible will switch type with their DTO type counterpart indirectly;<br/>
/// Should be used to convert <see cref="IEnumerable{T}"/> types with their <see cref="IEnumerable{DTO}"/> counterpart;<br/><br/>
/// If used with <see cref="HasConversionAttribute"/> this instance will be ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed public class HasIndirectConversionAttribute : Attribute {
    public string? ConverterType { get; }
    public string MethodsName { get; }
    public string ConvertedType { get; }
    /// <summary>
    /// Create a <see cref="HasIndirectConversionAttribute"/> instance
    /// </summary>
    /// <param name="methodName">
    /// The name of the static method that will convert this object
    /// </param>
    /// <param name="convertedType">
    /// The type that this object will be converted to and from
    /// </param>
    public HasIndirectConversionAttribute(string methodName, string convertedType) {
        ConverterType = null;
        MethodsName = methodName;
        ConvertedType = convertedType;
    }
    /// <summary>
    /// Create a <see cref="HasIndirectConversionAttribute"/> instance<br/>
    /// <paramref name="converterType"/> have to contain two public static method named <paramref name="methodName"/>,
    /// with only one argument, those being the current type and <paramref name="convertedType"/>
    /// </summary>
    /// <param name="converterType"> 
    /// The type which the conversion methods
    /// </param>
    /// <param name="methodName">
    /// The name of method that will convert this object
    /// </param>
    /// <param name="convertedType">
    /// The type's fully qualified name that this object will be converted to and from
    /// </param>
    public HasIndirectConversionAttribute(string converterType, string methodName, string convertedType) {
        ConverterType = converterType;
        MethodsName = methodName;
        ConvertedType = convertedType;
    }
    #region Non-public Methods
    internal static HasIndirectConversionAttribute GetAttributeFromData(AttributeData data) {
        if (data.FieldArguments.Any(keyvalue => keyvalue.value is null))
            throw new ArgumentNullException("HasConversionAttribute cannot have null arguments");

        if (data.FieldArguments.Length == 2)
            return GetAttributeWith2Args(data);
        return GetAttributeWith3Args(data);
    }
    private static HasIndirectConversionAttribute GetAttributeWith2Args(AttributeData data) {
        var arguments = new (string name, object? value)[2] {
            ("methodName", null),
            ("convertedType", null)
        };

        for (int i = 0; i < 2; i++) {
            var (name, value) = data.FieldArguments[i];
            int index = data.FieldArguments.ToList().FindIndex(t => t.name == name);

            index = index != -1 ? i : index;
            arguments[index].value = value;
        }
        return new HasIndirectConversionAttribute((string)arguments[0].value!, (string)arguments[1].value!);
    }
    private static HasIndirectConversionAttribute GetAttributeWith3Args(AttributeData data) {
        var arguments = new (string name, object? value)[3] {
            ("converterType", null),
            ("methodName", null),
            ("convertedType", null)
        };

        for (int i = 0; i < 2; i++) {
            var (name, value) = data.FieldArguments[i];
            int index = data.FieldArguments.ToList().FindIndex(t => t.name == name);

            index = index != -1 ? i : index;
            arguments[index].value = value;
        }
        return new HasIndirectConversionAttribute((string)arguments[0].value!, (string)arguments[1].value!, (string)arguments[2].value!);
    }
    #endregion
}
