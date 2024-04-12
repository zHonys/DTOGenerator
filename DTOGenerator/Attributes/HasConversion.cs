using SynoLib.Generators.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SynoLib.Generators.Attributes;

/// <summary>
/// Specifies that this varible will swich type in this class DTO
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed public class HasConversionAttribute : Attribute {
    public HasConversionForm ConversionForm { get; }
    public string ConvertedType { get; }
    /// <summary>
    /// Create a <see cref="HasConversionAttribute"/> instance<br/>
    /// </summary>
    /// <param name="hasConversionForm"> 
    /// specifies how the object will be converted;
    /// </param>
    /// <param name="convertedType">
    /// The type's fully qualified name that this object will be converted to and from
    /// </param>
    public HasConversionAttribute(HasConversionForm hasConversionForm, string convertedType) {
        ConversionForm = hasConversionForm;
        ConvertedType = convertedType;
    }
    #region Non-public Methods
    internal static HasConversionAttribute GetAttributeFromData(AttributeData data) {
        if (data.FieldArguments.Any(keyvalue => keyvalue.value is null))
            throw new ArgumentNullException("HasConversionAttribute cannot have null arguments");

        var arguments = new (string name, object? value)[2] {
                ("hasConversionForm", null),
                ("dtoType", null)
        };

        for (int i = 0; i < 2; i++) {
            var arg = data.FieldArguments[i];
            int index = data.FieldArguments.ToList().FindIndex(t => t.name == arg.name);

            index = index != -1 ? i : index;
            arguments[index].value = arg.value;
        }
        return new HasConversionAttribute((HasConversionForm)arguments[0].value!, (string)arguments[1].value!);
    }
    #endregion
}

/// <summary>
/// Specifies how the object will be converted to DTO/Model;<br/>
/// </summary>
public enum HasConversionForm {
    /// <summary>Variable will be converted explicitly.</summary>
    Explicit = 0,
    /// <summary> Variable will be converted implicitly.</summary>
    Implicit = 1,
    /// <summary> Variable will be converted with the static method created in the DTO.</summary>
    StaticMethods = 2
}