using System;

namespace SynoLib.Generators.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class DTOIgnoreAttribute : Attribute {
    public DTOIgnoreAttribute() {

    }
}
