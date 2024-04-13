using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SynoLib.Generators.DTOGenerator.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SynoLib.Generators.DTOGenerator.Visitors;
internal sealed class AttributeVisitor : CSharpSyntaxVisitor<List<AttributeData>> {
    private readonly ImmutableHashSet<string> _attributeNames;
    private readonly SemanticModel _semantics;

    public List<AttributeData> AttributesData { get; } = [];

    public AttributeVisitor(SemanticModel semantics, params string[] attributeNames) {
        _semantics = semantics;
        _attributeNames = [.. attributeNames.Concat(attributeNames.Select(an => an.Replace("Attribute", "")))];
    }

    public override List<AttributeData>? VisitAttribute(AttributeSyntax node) {
        base.VisitAttribute(node);
        if (!_attributeNames.Contains(node.Name.ToString()))
            return null;

        var fieldArguments = new List<(string, object?)>();
        var propertyArguments = new List<(string, object?)>();

        var arguments = node.ArgumentList?.Arguments.ToArray() ?? [];
        foreach (var (syntax, index) in arguments.Select((s, i) => (s, i))) {
            var operation = _semantics.GetOperation(syntax.Expression);
            object? constantValue = operation?.ConstantValue.Value;
            if (operation?.Kind == OperationKind.TypeOf) {
                constantValue = ((ITypeOfOperation)operation).TypeOperand.ToString();
            }
            if (syntax.NameColon != null)
                fieldArguments.Add((syntax.NameColon.Name.ToString(), operation?.ConstantValue.Value));
            else if (syntax.NameEquals != null) {
                propertyArguments.Add((syntax.NameEquals.Name.ToString(), operation?.ConstantValue.Value));
            } else {
                fieldArguments.Add((index.ToString(), operation?.ConstantValue.Value));
            }
        }
        AttributesData.Add(new AttributeData {
            Target = node.Parent!.Parent!,
            TargetName = ((CSharpSyntaxNode)node.Parent!.Parent!).Accept(IdentifierNameVisitor.Instance)!,
            Attribute = node,
            AttributeName = node.Name.ToString().Replace("Attribute", ""),
            FieldArguments = [.. fieldArguments],
            PropertyArguments = [.. propertyArguments]
        });
        return [AttributesData.Last()];
    }
    public override List<AttributeData>? VisitAttributeList(AttributeListSyntax node) {
        base.VisitAttributeList(node);
        List<AttributeData>? attributes = new();
        foreach (AttributeSyntax attribute in node.Attributes)
            attributes.AddRange(attribute.Accept(this) ?? new());
        return attributes;
    }
    public override List<AttributeData>? VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
        return node.AttributeLists.SelectMany(al => al.Accept(this)).ToList();
    }
    public override List<AttributeData>? VisitFieldDeclaration(FieldDeclarationSyntax node) {
        return node.AttributeLists.SelectMany(al => al.Accept(this)).ToList();
    }
}

internal struct AttributeData {
    public SyntaxNode Target { get; set; }
    public string TargetName { get; set; }
    public AttributeSyntax Attribute { get; set; }
    public string AttributeName { get; set; }
    public (string name, object? value)[] FieldArguments { get; set; }
    public (string name, object? value)[] PropertyArguments { get; set; }
}
