using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using SynoLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using SynoLib.Generators.Visitors;

namespace SynoLib.Generators;

internal record struct DTOModelData {
    public string ModelName { get; set; }
    public string DTOName { get; set; }

    public List<string> MemberNames { get; set; }
    public List<string> IgnoredRequired { get; set; }
    public SyntaxList<MemberDeclarationSyntax> Members { get; set; }
    public List<MemberHasConversion> WithConversion { get; set; }
    public List<MemberHasIndirectConversion> WithIndirectConversion { get; set; }

    public ConversionForm conversion;

    public static DTOModelData GetDTOModelData(Compilation compilation, ClassDeclarationSyntax cls) {
        var semantics = compilation.GetSemanticModel(cls.SyntaxTree);
        
        
        var HasDTOAttr = GetHasDTOAttribute(semantics, cls);
        var modelMemberVisitor = GetPropertiesAndFields(cls, semantics);
        var data = new DTOModelData() {
            ModelName = cls.Identifier.ValueText,
            DTOName = HasDTOAttr.DTOClassName.Replace("[class]", cls.Identifier.ValueText),
            conversion = HasDTOAttr.ConversionForm,
            MemberNames = modelMemberVisitor.MemberNames,
            Members = new(modelMemberVisitor.Members),
            WithConversion = modelMemberVisitor.MembersWithConversion,
            WithIndirectConversion = modelMemberVisitor.MembersWithIndirectConversion,
            IgnoredRequired = modelMemberVisitor.IgnoredRequiredMembersNames
        };
        return data;
    }

    private static HasDTOAttribute GetHasDTOAttribute(SemanticModel semantics, ClassDeclarationSyntax cls) {
        AttributeVisitor visitor = new (semantics,  [nameof(HasDTOAttribute)]);
        cls.AttributeLists.ToList().ForEach(al => al.Accept(visitor));
        var attrData = visitor.AttributesData.First(ad => ad.AttributeName == "HasDTO");
        return HasDTOAttribute.GetAttributeFromData(attrData);
    }

    private static ModelMemberVisitor GetPropertiesAndFields(ClassDeclarationSyntax cls, SemanticModel semantics) {
        SyntaxList<MemberDeclarationSyntax> members = cls.Members;
        List<string> ignoredRequired = new();
        var attributeVisitor = new AttributeVisitor(semantics, 
            [nameof(DTOIgnoreAttribute), nameof(HasConversionAttribute), nameof(HasIndirectConversionAttribute)]);
        var visitor = new ModelMemberVisitor(attributeVisitor);
        cls.Accept(visitor);
        return visitor;
    }
}