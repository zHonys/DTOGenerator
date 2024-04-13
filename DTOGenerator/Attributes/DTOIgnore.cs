using System;

namespace SynoLib.Generators.DTOGenerator.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class DTOIgnoreAttribute : Attribute {
    public DTOIgnoreAttribute() {

    }
}
