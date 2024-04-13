using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SynoLib.Generators.DTOGenerator.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace SynoLib.Generators.DTOGenerator.Visitors;
internal class ModelMemberVisitor : CSharpSyntaxVisitor{
    private readonly AttributeVisitor _attributeVisitor;
    public List<MemberDeclarationSyntax> Members { get; } = [];
    public List<string> MemberNames { get; } = [];

    public List<string> IgnoredRequiredMembersNames { get; } = [];

    public List<MemberHasConversion> MembersWithConversion { get; } = [];
    public List<MemberHasIndirectConversion> MembersWithIndirectConversion { get; } = [];

    public ModelMemberVisitor(AttributeVisitor visitor) {
        _attributeVisitor = visitor;
    }
    public override void VisitClassDeclaration(ClassDeclarationSyntax node) {
        base.VisitClassDeclaration(node);
        if (Members.Count > 0) return;

        foreach (var member in node.Members) {
            member.Accept(this);
        }
    }
    public override void VisitStructDeclaration(StructDeclarationSyntax node) {
        base.VisitStructDeclaration(node);
        if (Members.Count > 0) return;

        foreach (var member in node.Members) {
            member.Accept(this);
        }
    }
    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
        base.VisitPropertyDeclaration(node);
        var attributes = node.Accept(_attributeVisitor);

        node = node.WithModifiers(SyntaxFactory.TokenList(node.Modifiers.Where(m => m.RawKind != (int)SyntaxKind.VirtualKeyword)));
        if (attributes?.Count == 0) {
            Members.Add(node);
            if (node.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) is true)
                MemberNames.Add(node.Accept(IdentifierNameVisitor.Instance)!);
        }
        
        if (HasAttribute(attributes, "DTOIgnore") &&
            node.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.RequiredKeyword)) {
            IgnoredRequiredMembersNames.Add(node.Identifier.Text);
            return;
        }

        node = RemoveGeneratorAttributesProperty(node);
        if (TryGetSingleAttribute(attributes!, "HasConversion", out AttributeData attributeData)) {
            var attribute = HasConversionAttribute.GetAttributeFromData(attributeData);
            var updatedNode = UpdatePropertyType(node, attribute.ConvertedType);
            MembersWithConversion.Add(new(attributeData, node.Type, updatedNode, attribute));
            return;
        }
        if (TryGetSingleAttribute(attributes!, "HasIndirectConversion", out attributeData)) {
            var attribute = HasIndirectConversionAttribute.GetAttributeFromData(attributeData);
            var updatedNode = UpdatePropertyType(node, attribute.ConvertedType);
            MembersWithIndirectConversion.Add(new(attributeData, node.Type, updatedNode, attribute));
            return;
        }
    }
    public override void VisitFieldDeclaration(FieldDeclarationSyntax node) {
        base.VisitFieldDeclaration(node);
        var attributes = node.Accept(_attributeVisitor);

        node = node.WithModifiers(SyntaxFactory.TokenList(node.Modifiers.Where(m => m.RawKind != (int)SyntaxKind.VirtualKeyword)));
        if (attributes?.Count == 0)
            Members.Add(node);
        if (HasAttribute(attributes, "DTOIgnore") &&
            node.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.RequiredKeyword)) {
            IgnoredRequiredMembersNames.Add(node.Declaration.Variables[0].Identifier.Text);
            return;
        }
        node = RemoveGeneratorAttributesField(node);
        if (TryGetSingleAttribute(attributes!, "HasConversion", out AttributeData attributeData)) {
            var attribute = HasConversionAttribute.GetAttributeFromData(attributeData);
            var updatedNode = UpdateFieldyType(node, attribute.ConvertedType);
            
            MembersWithConversion.Add(new(attributeData, node.Declaration.Type, updatedNode, attribute));
            return;
        }
        if (TryGetSingleAttribute(attributes!, "HasIndirectConversion", out attributeData)) {
            var attribute = HasIndirectConversionAttribute.GetAttributeFromData(attributeData);
            var updatedNode = UpdateFieldyType(node, attribute.ConvertedType);
            MembersWithIndirectConversion.Add(new(attributeData, node.Declaration.Type, updatedNode, attribute));
            return;
        }
    }
    #region Private Methods
    private bool HasAttribute(List<AttributeData>? data, string attributeName) {
        return data.Any(a => a.AttributeName.Equals(attributeName));
    }
    private bool TryGetSingleAttribute(IEnumerable<AttributeData> attributes, string attributeName, out AttributeData attributeData) {
        try {
            attributeData = attributes.Single(a => a.AttributeName.Equals(attributeName));
            return true;
        } catch (Exception) {
            attributeData = new();
            return false;
        }
    }
    private PropertyDeclarationSyntax UpdatePropertyType(PropertyDeclarationSyntax node, string typeName) {
        return node.WithType(SyntaxFactory.ParseTypeName(typeName, consumeFullText: true).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")));
    }
    private FieldDeclarationSyntax UpdateFieldyType(FieldDeclarationSyntax node, string typeName) {
        var newType = SyntaxFactory.ParseTypeName(typeName, consumeFullText: true);
        return node.WithDeclaration(node.Declaration.WithType(newType).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")));
    }
    private PropertyDeclarationSyntax RemoveGeneratorAttributesProperty(PropertyDeclarationSyntax node) {
        HashSet<string> names = ["HasConversion", "HasConversionAttribute", "HasIndirectConversion", "HasIndirectConversionAttribute"];
        var attributes = node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => !names.Contains(a.Name.ToString()));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes));
        if (attributeList.Attributes.Count > 0)
            return node.WithAttributeLists(SyntaxFactory.List([attributeList]));
        return node.WithAttributeLists(new());
    }
    private FieldDeclarationSyntax RemoveGeneratorAttributesField(FieldDeclarationSyntax node) {
        HashSet<string> names = ["HasConversion", "HasConversionAttribute", "HasIndirectConversion", "HasIndirectConversionAttribute"];
        var attributes = node.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => !names.Contains(a.Name.ToString()));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes));
        if (attributeList.Attributes.Count > 0)
            return node.WithAttributeLists(SyntaxFactory.List([attributeList]));
        return node.WithAttributeLists(new());
    }
    #endregion
}

internal record struct MemberHasConversion {
    public AttributeData AttrData { get; set; }
    public TypeSyntax TargetType { get; set; }
    public MemberDeclarationSyntax Member { get; set; }
    public HasConversionAttribute Attribute { get; set; }
    public MemberHasConversion(AttributeData attributeData, TypeSyntax targetType, MemberDeclarationSyntax member, HasConversionAttribute attribute) {
        AttrData = attributeData;
        TargetType = targetType;
        Member = member;
        Attribute = attribute;
    }
}
internal record struct MemberHasIndirectConversion {
    public AttributeData AttrData { get; set; }
    public TypeSyntax TargetType { get; set; }
    public MemberDeclarationSyntax Member { get; set; }
    public HasIndirectConversionAttribute Attribute { get; set; }
    public MemberHasIndirectConversion(AttributeData attributeData, TypeSyntax targetType, MemberDeclarationSyntax member, HasIndirectConversionAttribute attribute) {
        AttrData = attributeData;
        TargetType = targetType;
        Member = member;
        Attribute = attribute;
    }
}