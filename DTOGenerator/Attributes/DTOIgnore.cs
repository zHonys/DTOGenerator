﻿using System;

namespace DTOGenerator.Attributes;

[System.AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class DTOIgnoreAttribute : Attribute {
    public DTOIgnoreAttribute() {
        
    }
}
