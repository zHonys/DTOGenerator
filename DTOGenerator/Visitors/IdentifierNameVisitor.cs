using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SynoLib.Generators.Visitors;
internal class IdentifierNameVisitor : CSharpSyntaxVisitor<string> {
    private IdentifierNameVisitor() { }
    public static IdentifierNameVisitor Instance = new();
    public override string VisitClassDeclaration(ClassDeclarationSyntax node) =>
        node.Identifier.Text;
    public override string VisitStructDeclaration(StructDeclarationSyntax node) =>
        node.Identifier.Text;
    public override string VisitPropertyDeclaration(PropertyDeclarationSyntax node) =>
        node.Identifier.Text;
    public override string? VisitFieldDeclaration(FieldDeclarationSyntax node)
        => node.Declaration.Variables[0].Identifier.Text;

}
