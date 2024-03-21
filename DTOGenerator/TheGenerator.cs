using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SynoLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        return attributeList.Any(a => a.ToString() == "HasDTO");
    }

    private static void Execute(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple) {
        var (compilation, list) = tuple;

        HashSet<ClassDeclarationSyntax> classes = new();
        foreach (var cls in list) {
            SemanticModel model = compilation.GetSemanticModel(cls.SyntaxTree);
            foreach (var attribute in cls.AttributeLists.SelectMany(a => a.Attributes)) {
                TypeInfo info = model.GetTypeInfo(attribute);
                if (info.Type?.ToString() == typeof(HasDTOAttribute).FullName) {
                    classes.Add(cls);
                    break;
                }
            }
        }

        StringBuilder builder = new();

        builder.Append("namespace SynoLib;\n");
        builder.Append(FetchAllNonGeneratorUsings(compilation.SyntaxTrees));
        builder.AppendLine();
        foreach (var cls in list) {

            SyntaxList<MemberDeclarationSyntax> members = new();
            foreach (var member in cls.Members) {
                if (member is FieldDeclarationSyntax or PropertyDeclarationSyntax)
                    members = members.Add(member);
            }

            var newDTO = CreateDTOClass(compilation, cls);

            builder.Append(newDTO);
        }


        context.AddSource("SynoLib.g.cs", builder.ToString());
    }
    private static SyntaxList<UsingDirectiveSyntax> FetchAllNonGeneratorUsings(IEnumerable<SyntaxTree> syntaxTrees) {
        var usingDirectives = 
            syntaxTrees.SelectMany(syntaxTree => {
                if (!syntaxTree.TryGetRoot(out SyntaxNode? node) || node == null)
                    return new List<UsingDirectiveSyntax>();
                return node.DescendantNodes().OfType<UsingDirectiveSyntax>();
            }).Distinct().ToList();
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


    private static ClassDeclarationSyntax CreateDTOClass(Compilation compilation, ClassDeclarationSyntax cls) {
        DTOModelData data = DTOModelData.GetDTOModelData(cls);
        var tokenList = new SyntaxTokenList(SyntaxFactory.Token(whiteSpace, SyntaxKind.PublicKeyword, new()),
                                            SyntaxFactory.Token(whiteSpace, SyntaxKind.SealedKeyword, whiteSpace));

        SyntaxList<UsingDirectiveSyntax> usings = new(cls.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>());

        SyntaxToken identifier = SyntaxFactory.Identifier(whiteSpace, $"DTO<{cls.Identifier}>", new());

        SyntaxList<MemberDeclarationSyntax> members = new();
        foreach (var member in cls.Members) {
            if (member is FieldDeclarationSyntax or PropertyDeclarationSyntax)
                members = members.Add(member);
        }
        members = members.AddRange(CreateConversions(data));

        return SyntaxFactory.ClassDeclaration(new(), tokenList, identifier, null, null, null, new(), members);
    }

    #region Conversions

    private static SeparatedSyntaxList<MemberDeclarationSyntax> CreateConversions(DTOModelData data) {
        SeparatedSyntaxList<MemberDeclarationSyntax> members = new();

        TypeSyntax DTOTypeSyntax = SyntaxFactory.ParseTypeName(data.DTOName);
        SyntaxToken DTOParameterToken = SyntaxFactory.Identifier("dto");
        ParameterListSyntax DTOParameterSyntax = SyntaxFactory.ParameterList().AddParameters(
            SyntaxFactory.Parameter(
                new(), new SyntaxTokenList(),
                DTOTypeSyntax.WithTrailingTrivia(whiteSpace),
                DTOParameterToken,
                null));

        TypeSyntax ModelTypeSyntax = SyntaxFactory.ParseTypeName($"{data.ModelName}");
        SyntaxToken ModelParameterToken = SyntaxFactory.Identifier("model");
        ParameterListSyntax ModelParameterSyntax = SyntaxFactory.ParameterList().AddParameters(
            SyntaxFactory.Parameter(
                new(), new SyntaxTokenList(),
                ModelTypeSyntax.WithTrailingTrivia(whiteSpace),
                ModelParameterToken,
                null));


        if (data.conversion.HasFlag(ConversionForm.Explicit)) {
            var ModeltoDTO = SyntaxFactory.ConversionOperatorDeclaration(
                new(),
                publicStatic,
                SyntaxFactory.Token(new(), SyntaxKind.ExplicitKeyword, whiteSpace),
                DTOTypeSyntax.WithLeadingTrivia(whiteSpace),
                ModelParameterSyntax,
                CreateConversionBodies(data),
                null);
            var DTOtoModel = SyntaxFactory.ConversionOperatorDeclaration(
                new(),
                publicStatic,
                SyntaxFactory.Token(new(), SyntaxKind.ExplicitKeyword, whiteSpace),
                ModelTypeSyntax.WithLeadingTrivia(whiteSpace),
                DTOParameterSyntax,
                null,
                null);
            members = members.AddRange([DTOtoModel, ModeltoDTO]);
        }

        return members;
    }

    private static BlockSyntax CreateConversionBodies(DTOModelData data) {
        SeparatedSyntaxList<ExpressionSyntax> assignments = new();
        foreach (var property in data.PropertiesNames) {
            var modelProperty = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.IdentifierName("model"), SyntaxFactory.Token(SyntaxKind.DotToken), SyntaxFactory.IdentifierName(property));
            assignments = assignments.Add(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression, 
                    SyntaxFactory.IdentifierName(property), SyntaxFactory.Token(SyntaxKind.EqualsToken), modelProperty));

        }
        var initializer = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, expressions: assignments);
        var dtoCreation = SyntaxFactory.ImplicitObjectCreationExpression(
            SyntaxFactory.Token(SyntaxKind.NewKeyword).WithLeadingTrivia(whiteSpace),
            SyntaxFactory.ArgumentList(),
            initializer);
        var block = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(dtoCreation));
        return block;
    }

    #endregion

    #region Random functions


    #endregion

    #region Static Data

    private static SyntaxTriviaList whiteSpace = new(SyntaxFactory.Whitespace(" "));
    private static SyntaxTriviaList endLine = new(SyntaxFactory.EndOfLine("\n"));

    private static SyntaxTokenList publicStatic = new(NewToken(new(), SyntaxKind.PublicKeyword, whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, whiteSpace));
    private static SyntaxTokenList @operator = new(NewToken(new(), SyntaxKind.OperatorKeyword, whiteSpace));

    #endregion

    #region SyntaxFactory Simplifiers

    private static SyntaxToken NewToken(SyntaxKind kind) => SyntaxFactory.Token(kind);
    private static SyntaxToken NewToken(SyntaxTriviaList leading, SyntaxKind kind, SyntaxTriviaList trailing) => SyntaxFactory.Token(leading, kind, trailing);

    #endregion
}

internal record struct DTOModelData {
    public string ModelName { get; set; }
    public string DTOName => $"DTO<{ModelName}>";

    public List<string> PropertiesNames { get; set; }

    public ConversionForm conversion;
    
    public static DTOModelData GetDTOModelData(ClassDeclarationSyntax cls) {
        var data = new DTOModelData() {
            ModelName = cls.Identifier.ValueText,
            conversion = ConversionForm.Explicit
        };
        return data;
    }
}