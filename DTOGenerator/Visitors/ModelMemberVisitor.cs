using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SynoLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SynoLib.Generators.Visitors;
internal class ModelMemberVisitor : CSharpSyntaxVisitor{
    private readonly AttributeVisitor _attributeVisitor;
    public List<MemberDeclarationSyntax> Members { get; } = [];
    public List<string> MemberNames => Members.Select(x => x.Accept(IdentifierNameVisitor.Instance) ?? "").ToList();

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
        if (attributes?.Count == 0)
            Members.Add(node);
        if (HasAttribute(attributes, "DTOIgnore") && 
            node.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.RequiredKeyword))) {
            IgnoredRequiredMembersNames.Add(node.Identifier.Text);
            return;
        }
        if (HasAttribute(attributes, "HasConversion")) {
            var attribute = HasConversionAttribute.GetAttributeFromData(
                attributes.Single(a => a.AttributeName == "HasConversion"));
            MembersWithConversion.Add(new(node, attribute));
            return;
        }
        if (HasAttribute(attributes, "HasIndirectConversion")) {
            var attribute = HasIndirectConversionAttribute.GetAttributeFromData(
                attributes.Single(a => a.AttributeName == "HasIndirectConversion"));
            MembersWithIndirectConversion.Add(new(node, attribute));
            return;
        }
    }
    public override void VisitFieldDeclaration(FieldDeclarationSyntax node) {
        base.VisitFieldDeclaration(node);
        var attributes = node.Accept(_attributeVisitor);
        if (attributes?.Count == 0)
            Members.Add(node);
        if (HasAttribute(attributes, "DTOIgnore") &&
            node.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.RequiredKeyword))) {
            IgnoredRequiredMembersNames.Add(node.Declaration.Variables[0].Identifier.Text);
            return;
        }
        if (HasAttribute(attributes, "HasConversion")) {
            var attribute = HasConversionAttribute.GetAttributeFromData(
                attributes.Single(a => a.AttributeName == "HasConversion"));
            MembersWithConversion.Add(new(node, attribute));
            return;
        }
        if (HasAttribute(attributes, "HasIndirectConversion")) {
            var attribute = HasIndirectConversionAttribute.GetAttributeFromData(
                attributes.Single(a => a.AttributeName == "HasIndirectConversion"));
            MembersWithIndirectConversion.Add(new(node, attribute));
            return;
        }
    }

    private bool HasAttribute(List<AttributeData>? data, string attributeName) {
        return data.Any(a => a.AttributeName.Equals(attributeName));
    }
}

public record struct MemberHasConversion {
    public MemberDeclarationSyntax Member { get; set; }
    public HasConversionAttribute Attribute { get; set; }
    public MemberHasConversion(MemberDeclarationSyntax member, HasConversionAttribute attribute) {
        Member = member;
        Attribute = attribute;
    }
}
public record struct MemberHasIndirectConversion {
    public MemberDeclarationSyntax Member { get; set; }
    public HasIndirectConversionAttribute Attribute { get; set; }
    public MemberHasIndirectConversion(MemberDeclarationSyntax member, HasIndirectConversionAttribute attribute) {
        Member = member;
        Attribute = attribute;
    }
}