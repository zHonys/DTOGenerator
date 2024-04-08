using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using SynoLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace SynoLib.Generators;

internal record struct DTOModelData {
    public string ModelName { get; set; }
    public string DTOName { get; set; }

    public List<string> PropertiesNames { get; set; }
    public List<string> IgnoredRequired { get; set; }
    public SyntaxList<MemberDeclarationSyntax> Members { get; set; }

    public ConversionForm conversion;

    public static DTOModelData GetDTOModelData(Compilation compilation, ClassDeclarationSyntax cls) {
        var semantics = compilation.GetSemanticModel(cls.SyntaxTree);

        var HasDTOAttr = GetHasDTOAttribute(cls, semantics);
        var (members, IgnoredRequired) = GetPropertiesAndFields(cls);
        var data = new DTOModelData() {
            ModelName = cls.Identifier.ValueText,
            DTOName = HasDTOAttr.DTOClassName.Replace("[class]", cls.Identifier.ValueText),
            conversion = HasDTOAttr.ConversionForm,
            Members = members,
            IgnoredRequired = IgnoredRequired
        };
        return data with { PropertiesNames = GetMembersNames(data.Members) };
    }

    #region Get HasDTO Data
    private static HasDTOAttribute GetHasDTOAttribute(ClassDeclarationSyntax cls, SemanticModel semantics) {
        var attributeSyntax = cls.AttributeLists
            .SelectMany(al => al.Attributes)
            .Single(a =>
                Regex.IsMatch(a.Name.ToString(), $"^HasDTOAttribute$|^HasDTO$"));

        return GetHasDTOData(semantics, attributeSyntax);
    }
    private static HasDTOAttribute GetHasDTOData(SemanticModel semantics, AttributeSyntax attributeSyntax) {
        Dictionary<string, object> HasDTOAttributeData = new() {
            ["dtoClassname"] = "[class]DTO",
            ["conversionForm"] = ConversionForm.Explicit
        };
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attributeSyntax.ArgumentList?.Arguments ?? new();
        foreach (var (argument, index) in arguments.Select((a, i) => (a, i))) {
            var operation = semantics.GetOperation(argument.Expression)!;
            if (argument.NameColon is null) {
                HasDTOAttributeData[HasDTOAttributeData.ElementAt(index).Key] = operation.ConstantValue.Value!;
            } else if (HasDTOAttributeData.ContainsKey(argument.NameColon.Name.ToString())) {
                HasDTOAttributeData[argument.NameColon.Name.ToString()] = operation.ConstantValue.Value!;
            }
        }
        return new HasDTOAttribute(
            (string)HasDTOAttributeData["dtoClassname"],
            (ConversionForm)HasDTOAttributeData["conversionForm"]);
    }
    #endregion

    private static (SyntaxList<MemberDeclarationSyntax>, List<string>) GetPropertiesAndFields(ClassDeclarationSyntax cls) {
        SyntaxList<MemberDeclarationSyntax> members = new();
        List<string> ignoredRequired = new();
        foreach (var member in cls.Members) {
            if (member is not (FieldDeclarationSyntax or PropertyDeclarationSyntax))
                continue;

            var attributes = member.AttributeLists.SelectMany(al => al.Attributes).Select(a => ((IdentifierNameSyntax)a.Name).Identifier.ValueText).ToList();
            if (!attributes.Any(a => a is "DTOIgnore" or "DTOIgnoreAttribute"))
                members = members.Add(member);
            else if(member.Modifiers.Any(m => m.Text.Equals("required"))) {
                if (member is PropertyDeclarationSyntax property)
                    ignoredRequired.Add(property.Identifier.Text);
                else if (member is FieldDeclarationSyntax field)
                    ignoredRequired.Add(field.Declaration.Variables[0].Identifier.Text);
            }
        }
        return (members, ignoredRequired);
    }
    private static List<string> GetMembersNames(SyntaxList<MemberDeclarationSyntax> members) {
        List<string> names = new();
        foreach (var item in members) {
            if (item is PropertyDeclarationSyntax propertySyntax) {
                if (propertySyntax.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.GetKeyword)) is true)
                    names.Add(propertySyntax.Identifier.ToString());
            }
        }
        return names;
    }
}