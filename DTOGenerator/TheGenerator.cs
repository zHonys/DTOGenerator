using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SynoLib.Generators.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SynoLib.Generators;

[Generator]
internal sealed class TheGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: ShouldHaveDTO,
            transform: static (ctx,  _) => (ClassDeclarationSyntax)ctx.Node
        ).Where(m => m is not null);

        var compilation = context.CompilationProvider.Combine(provider.Collect());

        context.RegisterSourceOutput(compilation, Execute);
    }

    private static bool ShouldHaveDTO(SyntaxNode node, CancellationToken cancellationToken) {
        if (node is not ClassDeclarationSyntax cls)
            return false;

        var attributeList = cls.AttributeLists.SelectMany(al => al.Attributes);
        return attributeList.Any(a => a.Name.ToString() == "HasDTO");
    }

    private static void Execute(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple) {
        var (compilation, list) = tuple;
        
        StringBuilder builder = new();

        builder.Append(GetUsings(list));
        builder.AppendLine();
        var groups = from c in list
                     group c by GetNamespace(c);

        foreach (var namespaceGroup in groups) {
            SyntaxList<MemberDeclarationSyntax> members = new();
            foreach (var cls in namespaceGroup) 
                members = members.Add(CreateDTOClass(compilation, cls));

            NameSyntax name = SyntaxFactory.ParseName(namespaceGroup.Key).WithLeadingTrivia(_whiteSpace);
            builder.AppendLine(SyntaxFactory.NamespaceDeclaration(name, new(), new(), members).ToString());
        }

        context.AddSource("SynoLibDTOGenerator.g.cs", builder.ToString());
    }

    private static ClassDeclarationSyntax CreateDTOClass(Compilation compilation, ClassDeclarationSyntax cls) {
        DTOModelData data = DTOModelData.GetDTOModelData(compilation, cls);

        var tokenList = new SyntaxTokenList(SyntaxFactory.Token(_whiteSpace, SyntaxKind.PublicKeyword, new()),
                                            SyntaxFactory.Token(_whiteSpace, SyntaxKind.SealedKeyword, _whiteSpace));


        SyntaxToken identifier = SyntaxFactory.Identifier(_whiteSpace, data.DTOName, new());

        SyntaxList<MemberDeclarationSyntax> members = data.Members;
        members = members.AddRange(CreateConversions(data));

        return SyntaxFactory.ClassDeclaration(new(), tokenList, identifier, null, null, null, new(), members);
    }

    #region Using and Namespace

    private static string GetNamespace(ClassDeclarationSyntax cls) {
        if (cls.Parent is not BaseNamespaceDeclarationSyntax namespaceSyntax)
            return "global";
        return namespaceSyntax.Name.ToString();
    }

    private static SyntaxList<UsingDirectiveSyntax> GetUsings(IEnumerable<ClassDeclarationSyntax> classes) {
        var usingDirectives =
            classes.SelectMany(c => ((CompilationUnitSyntax)c.SyntaxTree.GetRoot()).Usings)
                   .Distinct();

        usingDirectives = usingDirectives.Where(u => {
            var name = u.NamespaceOrType;
            if (name is AliasQualifiedNameSyntax alias)
                return !alias.Name.ToString().StartsWith("SynoLib");
            if (name is IdentifierNameSyntax identifier)
                return !identifier.ToString().StartsWith("SynoLib");
            if (name is QualifiedNameSyntax qualified)
                return !qualified.Left.ToString().StartsWith("SynoLib");

            return !name.ToString().StartsWith("SynoLib");
        }).ToList();

        return new SyntaxList<UsingDirectiveSyntax>(usingDirectives);
    }

    #endregion

    #region Conversions

    private static SyntaxList<MemberDeclarationSyntax> CreateConversions(DTOModelData data) {
        SyntaxList<MemberDeclarationSyntax> members = new();
        if (data.conversion == ConversionForm.None)
            return members;

        TypeSyntax DTOTypeSyntax = SyntaxFactory.ParseTypeName(data.DTOName);
        SyntaxToken DTOParameterToken = SyntaxFactory.Identifier("dto");
        ParameterListSyntax DTOParameterSyntax = SyntaxFactory.ParameterList().AddParameters(
            SyntaxFactory.Parameter(
                new(), new SyntaxTokenList(),
                DTOTypeSyntax.WithTrailingTrivia(_whiteSpace),
                DTOParameterToken,
                null));

        TypeSyntax ModelTypeSyntax = SyntaxFactory.ParseTypeName($"{data.ModelName}");
        SyntaxToken ModelParameterToken = SyntaxFactory.Identifier("model");
        ParameterListSyntax ModelParameterSyntax = SyntaxFactory.ParameterList().AddParameters(
            SyntaxFactory.Parameter(
                new(), new SyntaxTokenList(),
                ModelTypeSyntax.WithTrailingTrivia(_whiteSpace),
                ModelParameterToken,
                null));

        FieldDeclarationSyntax staticFunctoDTO = CreateStaticFuncConversor(data, "_toDTOFunc", data.DTOName, ModelParameterSyntax, false);
        FieldDeclarationSyntax staticFunctoModel = CreateStaticFuncConversor(data, "_toModelFunc", data.ModelName, DTOParameterSyntax, true);
        members = members.AddRange([staticFunctoDTO, staticFunctoModel]);
        var toDTOExpression = CreateArrowFuncCall("_toDTOFunc", ModelParameterSyntax);
        var toModelExpression = CreateArrowFuncCall("_toModelFunc", DTOParameterSyntax);

        if (data.conversion.HasFlag(ConversionForm.Explicit)) {
            ConversionOperatorDeclarationSyntax DTOToModel = CreateConversionOperator(
                SyntaxKind.ExplicitKeyword,
                ModelTypeSyntax, DTOParameterSyntax,
                toModelExpression);
            ConversionOperatorDeclarationSyntax ModelToDTO = CreateConversionOperator(
                SyntaxKind.ExplicitKeyword,
                DTOTypeSyntax, ModelParameterSyntax,
                toDTOExpression);
            members = members.AddRange([DTOToModel, ModelToDTO]);
        }
        else if (data.conversion.HasFlag(ConversionForm.Implicit)) {
            ConversionOperatorDeclarationSyntax DTOToModel = CreateConversionOperator(
                SyntaxKind.ImplicitKeyword,
                ModelTypeSyntax, DTOParameterSyntax,
                toModelExpression);
            ConversionOperatorDeclarationSyntax ModelToDTO = CreateConversionOperator(
                SyntaxKind.ImplicitKeyword,
                DTOTypeSyntax, ModelParameterSyntax,
                toDTOExpression);
            members = members.AddRange([DTOToModel, ModelToDTO]);
        }
        if(data.conversion.HasFlag(ConversionForm.StaticMethods)) {
            MethodDeclarationSyntax DTOToModel = CreateStaticConversion(
                "ToDTO",
                DTOTypeSyntax, ModelParameterSyntax,
                toDTOExpression);
            MethodDeclarationSyntax ModelToDTO = CreateStaticConversion(
                "ToModel",
                ModelTypeSyntax, DTOParameterSyntax,
                toModelExpression);
            members = members.AddRange([DTOToModel, ModelToDTO]);
        }
        if (data.conversion.HasFlag(ConversionForm.ReferenceMethods)) {
            MethodDeclarationSyntax DTOToModel = CreateReferenceConversion(
                "_toModelFunc",
                "ToModel",
                ModelTypeSyntax);
            members = members.Add(DTOToModel);
        }

        return members;
    }

    private static FieldDeclarationSyntax CreateStaticFuncConversor(DTOModelData data, string funcName, string returnTypeName, ParameterListSyntax parameterListSyntax, bool isToModel) {
        string paramName = parameterListSyntax.Parameters[0].Identifier.ValueText;
        string paramTypeName = parameterListSyntax.Parameters[0].Type!.ToString();
        var equalValueSyntax = SyntaxFactory.EqualsValueClause(SyntaxFactory.ParenthesizedLambdaExpression(parameterListSyntax, CreateStaticConversionBlock(data, paramName, isToModel)));
        var variableSyntax = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(funcName),null, equalValueSyntax);
        var varDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName($"Func<{paramTypeName}, {returnTypeName}>"), new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(variableSyntax));
        var staticFunctoDTO = SyntaxFactory.FieldDeclaration(new(), _privateStatic, varDeclaration);
        return staticFunctoDTO;
    }

    private static BlockSyntax CreateStaticConversionBlock(DTOModelData data, string paramName, bool isToModel) {
        SeparatedSyntaxList<ExpressionSyntax> assignments = new();
        foreach (string property in data.MemberNames) {
            var modelProperty = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.IdentifierName(paramName), SyntaxFactory.Token(SyntaxKind.DotToken), SyntaxFactory.IdentifierName(property));
            assignments = assignments.Add(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression, 
                    SyntaxFactory.IdentifierName(property), SyntaxFactory.Token(SyntaxKind.EqualsToken), modelProperty));

        }
        if(isToModel)
            foreach (string ignoredRequired in data.IgnoredRequired) {
                assignments = assignments.Add(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression, 
                        SyntaxFactory.IdentifierName(ignoredRequired),
                        SyntaxFactory.Token(SyntaxKind.EqualsToken),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
            }
        var initializer = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, expressions: assignments);
        var dtoCreation = SyntaxFactory.ImplicitObjectCreationExpression(
            SyntaxFactory.Token(SyntaxKind.NewKeyword).WithLeadingTrivia(_whiteSpace),
            SyntaxFactory.ArgumentList(),
            initializer);
        var block = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(dtoCreation));
        return block;
    }
    
    private static ArrowExpressionClauseSyntax CreateArrowFuncCall(string funcName, ParameterListSyntax parameters) {
        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameters.Parameters[0].Identifier.ValueText));
        var invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(funcName), SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(argument)));
        return SyntaxFactory.ArrowExpressionClause(invocation);
    }

    private static ConversionOperatorDeclarationSyntax CreateConversionOperator(
        SyntaxKind operatorKind, TypeSyntax returnType, ParameterListSyntax parameters, ArrowExpressionClauseSyntax expression) {
        return SyntaxFactory.ConversionOperatorDeclaration(
            new(),
            _publicStatic,
            SyntaxFactory.Token(new(), operatorKind, _whiteSpace),
            _operator,
            returnType.WithLeadingTrivia(_whiteSpace),
            parameters,
            null,
            expression,
            _semicolon);
    }

    private static MethodDeclarationSyntax CreateStaticConversion(
        string methodName, TypeSyntax returnType, ParameterListSyntax parameters, ArrowExpressionClauseSyntax expression) {
        return SyntaxFactory.MethodDeclaration(
            new(),
            _publicStatic,
            returnType.WithLeadingTrivia(_whiteSpace).WithTrailingTrivia(_whiteSpace),
            null,
            SyntaxFactory.Identifier(methodName),
            null,
            parameters,
            new(),
            null,
            expression,
            _semicolon);
    }

    private static MethodDeclarationSyntax CreateReferenceConversion(string funcName, string methodName, TypeSyntax returnType) {
        var argument = SyntaxFactory.Argument(SyntaxFactory.ThisExpression());
        var invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(funcName), SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(argument)));
        var expression = SyntaxFactory.ArrowExpressionClause(invocation);
        return SyntaxFactory.MethodDeclaration(
            new(),
            _publicStatic,
            returnType.WithLeadingTrivia(_whiteSpace).WithTrailingTrivia(_whiteSpace),
            null,
            SyntaxFactory.Identifier(methodName),
            null,
            SyntaxFactory.ParameterList(),
            new(),
            null,
            expression,
            _semicolon);
    }

    #endregion

    #region Random functions


    #endregion

    #region Static Data

    private static readonly SyntaxTriviaList _whiteSpace = new(SyntaxFactory.Whitespace(" "));
    private static readonly SyntaxTriviaList _endLine = new(SyntaxFactory.EndOfLine("\n"));

    private static readonly SyntaxTokenList _publicStatic = new(NewToken(new(), SyntaxKind.PublicKeyword, _whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, _whiteSpace));
    private static readonly SyntaxTokenList _internalStatic = new(NewToken(new(), SyntaxKind.InternalKeyword, _whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, _whiteSpace));
    private static readonly SyntaxTokenList _privateStatic = new(NewToken(new(), SyntaxKind.PrivateKeyword, _whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, _whiteSpace));

    private static readonly SyntaxToken _operator =  NewToken(new(), SyntaxKind.OperatorKeyword, _whiteSpace);
    private static readonly SyntaxToken _semicolon = NewToken(SyntaxKind.SemicolonToken);

    #endregion

    #region SyntaxFactory Simplifiers

    private static SyntaxToken NewToken(SyntaxKind kind) => SyntaxFactory.Token(kind);
    private static SyntaxToken NewToken(SyntaxTriviaList leading, SyntaxKind kind, SyntaxTriviaList trailing) => SyntaxFactory.Token(leading, kind, trailing);

    private static SyntaxTrivia NewTrivia(SyntaxKind trivia, string text) => SyntaxFactory.SyntaxTrivia(trivia, text);

    #endregion
}