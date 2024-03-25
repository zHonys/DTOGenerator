using DTOGenerator.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SynoLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        return attributeList.Any(a => a.Name.ToString() == "HasDTO");
    }

    private static void Execute(SourceProductionContext context, (Compilation Left, ImmutableArray<ClassDeclarationSyntax> Right) tuple) {
        var (compilation, list) = tuple;

        HashSet<ClassDeclarationSyntax> classes = new();
        foreach (var cls in list) {
            SemanticModel model = compilation.GetSemanticModel(cls.SyntaxTree);
            foreach (var attribute in cls.AttributeLists.SelectMany(a => a.Attributes)) {
                Microsoft.CodeAnalysis.TypeInfo info = model.GetTypeInfo(attribute);
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
        DTOModelData data = DTOModelData.GetDTOModelData(compilation, cls);

        var tokenList = new SyntaxTokenList(SyntaxFactory.Token(_whiteSpace, SyntaxKind.PublicKeyword, new()),
                                            SyntaxFactory.Token(_whiteSpace, SyntaxKind.SealedKeyword, _whiteSpace));

        SyntaxList<UsingDirectiveSyntax> usings = new(cls.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>());

        SyntaxToken identifier = SyntaxFactory.Identifier(_whiteSpace, $"DTO<{cls.Identifier}>", new());

        SyntaxList<MemberDeclarationSyntax> members = new();
        foreach (var member in cls.Members) {
            if (member is FieldDeclarationSyntax or PropertyDeclarationSyntax)
                members = members.Add(member);
        }
        members = members.AddRange(CreateConversions(data));

        return SyntaxFactory.ClassDeclaration(new(), tokenList, identifier, null, null, null, new(), members);
    }

    #region Conversions

    private static SyntaxList<MemberDeclarationSyntax> CreateConversions(DTOModelData data) {
        SyntaxList<MemberDeclarationSyntax> members = new();

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
        if(data.conversion != 0) {
            FieldDeclarationSyntax staticFunctoDTO = CreateStaticFuncConversor(data, "_toDTOFunc", "DTO<Product>", ModelParameterSyntax);
            FieldDeclarationSyntax staticFunctoModel = CreateStaticFuncConversor(data, "_toModelFunc", "Product", DTOParameterSyntax);
            members = members.AddRange([staticFunctoDTO, staticFunctoModel]);
        }

        if (data.conversion.HasFlag(ConversionForm.Explicit)) {
            ConversionOperatorDeclarationSyntax ModeltoDTO = CreateConversionOperator(
                "_toDTOFunc",
                SyntaxKind.ExplicitKeyword,
                DTOTypeSyntax, ModelParameterSyntax);
            ConversionOperatorDeclarationSyntax DTOtoModel = CreateConversionOperator(
                "_toModelFunc",
                SyntaxKind.ExplicitKeyword,
                ModelTypeSyntax, DTOParameterSyntax);
            members = members.AddRange([DTOtoModel, ModeltoDTO]);
        }
        if (data.conversion.HasFlag(ConversionForm.Implicit)) {
            ConversionOperatorDeclarationSyntax ModeltoDTO = CreateConversionOperator(
                "_toDTOFunc",
                SyntaxKind.ImplicitKeyword,
                DTOTypeSyntax, ModelParameterSyntax);
            ConversionOperatorDeclarationSyntax DTOtoModel = CreateConversionOperator(
                "_toModelFunc",
                SyntaxKind.ImplicitKeyword,
                ModelTypeSyntax, DTOParameterSyntax);
            members = members.AddRange([DTOtoModel, ModeltoDTO]);
        }

        return members;
    }

    private static FieldDeclarationSyntax CreateStaticFuncConversor(DTOModelData data, string funcName, string returnTypeName, ParameterListSyntax parameterListSyntax) {
        string paramName = parameterListSyntax.Parameters[0].Identifier.ValueText;
        string paramTypeName = parameterListSyntax.Parameters[0].Type!.ToString();
        var equalValueSyntax = SyntaxFactory.EqualsValueClause(SyntaxFactory.ParenthesizedLambdaExpression(parameterListSyntax, CreateStaticConversionBlock(data, paramName)));
        var variableSyntax = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(funcName), null, equalValueSyntax);
        var varDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName($"Func<{paramTypeName}, {returnTypeName}>"), new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(variableSyntax));
        var staticFunctoDTO = SyntaxFactory.FieldDeclaration(new(), _internalStatic, varDeclaration);
        return staticFunctoDTO;
    }

    private static ConversionOperatorDeclarationSyntax CreateConversionOperator(string funcName, SyntaxKind operatorKind, TypeSyntax DTOTypeSyntax, ParameterListSyntax ModelParameterSyntax) {
        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(ModelParameterSyntax.Parameters[0].Identifier.ValueText));
        var invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(funcName), SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>().Add(argument)));
        var expression = SyntaxFactory.ArrowExpressionClause(invocation);
        return SyntaxFactory.ConversionOperatorDeclaration(
            new(),
            _publicStatic,
            SyntaxFactory.Token(new(), operatorKind, _whiteSpace),
            _operator,
            DTOTypeSyntax.WithLeadingTrivia(_whiteSpace),
            ModelParameterSyntax,
            null,
            expression,
            _semicolon);
    }

    private static BlockSyntax CreateStaticConversionBlock(DTOModelData data, string paramName) {
        SeparatedSyntaxList<ExpressionSyntax> assignments = new();
        foreach (var property in data.PropertiesNames) {
            var modelProperty = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.IdentifierName(paramName), SyntaxFactory.Token(SyntaxKind.DotToken), SyntaxFactory.IdentifierName(property));
            assignments = assignments.Add(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression, 
                    SyntaxFactory.IdentifierName(property), SyntaxFactory.Token(SyntaxKind.EqualsToken), modelProperty));

        }
        var initializer = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, expressions: assignments);
        var dtoCreation = SyntaxFactory.ImplicitObjectCreationExpression(
            SyntaxFactory.Token(SyntaxKind.NewKeyword).WithLeadingTrivia(_whiteSpace),
            SyntaxFactory.ArgumentList(),
            initializer);
        var block = SyntaxFactory.Block(SyntaxFactory.ReturnStatement(dtoCreation));
        return block;
    }

    #endregion

    #region Random functions


    #endregion

    #region Static Data

    private static readonly SyntaxTriviaList _whiteSpace = new(SyntaxFactory.Whitespace(" "));
    private static readonly SyntaxTriviaList _endLine = new(SyntaxFactory.EndOfLine("\n"));

    private static readonly SyntaxTokenList _publicStatic = new(NewToken(new(), SyntaxKind.PublicKeyword, _whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, _whiteSpace));
    private static readonly SyntaxTokenList _internalStatic = new(NewToken(new(), SyntaxKind.InternalKeyword, _whiteSpace), NewToken(new(), SyntaxKind.StaticKeyword, _whiteSpace));
    private static readonly SyntaxToken _operator =  NewToken(new(), SyntaxKind.OperatorKeyword, _whiteSpace);
    private static readonly SyntaxToken _semicolon = NewToken(SyntaxKind.SemicolonToken);

    #endregion

    #region SyntaxFactory Simplifiers

    private static SyntaxToken NewToken(SyntaxKind kind) => SyntaxFactory.Token(kind);
    private static SyntaxToken NewToken(SyntaxTriviaList leading, SyntaxKind kind, SyntaxTriviaList trailing) => SyntaxFactory.Token(leading, kind, trailing);

    private static SyntaxTrivia NewTrivia(SyntaxKind trivia, string text) => SyntaxFactory.SyntaxTrivia(trivia, text);

    #endregion
}

internal record struct DTOModelData {
    public string ModelName { get; set; }
    public string DTOName => $"DTO<{ModelName}>";

    public List<string> PropertiesNames { get; set; }
    public SyntaxList<MemberDeclarationSyntax> Members { get; set; }

    public ConversionForm conversion;
    
    public static DTOModelData GetDTOModelData(Compilation compilation, ClassDeclarationSyntax cls) {
        var data = new DTOModelData() {
            ModelName = cls.Identifier.ValueText,
            conversion = GetConversionForm(compilation, cls),
            Members = GetPropertiesAndFields(cls)
        };
        return data with { PropertiesNames = data.Members.Select(m => m.ChildTokens().Single(t => t.IsKind(SyntaxKind.IdentifierToken)).ValueText).ToList() };
    }

    private static ConversionForm GetConversionForm(Compilation compilation, ClassDeclarationSyntax cls) {
        var semantics = compilation.GetSemanticModel(cls.SyntaxTree);
        var attributeSyntax = cls.AttributeLists.SelectMany(al => al.Attributes).Single(a => a.Name.ToString() == "HasDTO");
        var attribute = GetAttributeFromSyntax<HasDTOAttribute>(semantics, attributeSyntax);
        return attribute.ConversionForm;
    }

    private static TAttr GetAttributeFromSyntax<TAttr>(SemanticModel semanticModel, AttributeSyntax attributeSyntax) where TAttr : Attribute {
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments =  attributeSyntax.ArgumentList?.Arguments ?? new();
        List<string> typeNames = new();
        List<object> parameters = new();
        foreach (var argument in arguments) {
            var operation = semanticModel.GetOperation(argument.Expression)!;
            typeNames.Add(operation!.Type!.Name);
            parameters.Add(operation.ConstantValue!.Value!);
        }
        var constructors = typeof(TAttr).GetConstructors();
        var constructorInfo = constructors.Single(c => {
            var paramTypes = c.GetParameters().Select(p => p.ParameterType.Name).OrderBy(p => p);
            return paramTypes.SequenceEqual(typeNames.OrderBy(p => p));
        });
        return (TAttr)constructorInfo.Invoke([.. parameters]);
    }

    private static SyntaxList<MemberDeclarationSyntax> GetPropertiesAndFields(ClassDeclarationSyntax cls) {
        SyntaxList<MemberDeclarationSyntax> members = new();
        foreach (var member in cls.Members) {
            if (member is not (FieldDeclarationSyntax or PropertyDeclarationSyntax))
                continue;

            var attributes = member.AttributeLists.SelectMany(al => al.Attributes).Select(a => ((IdentifierNameSyntax)a.Name).Identifier.ValueText).ToList();
            if (!attributes.Any(a => a is "DTOIgnore" or "DTOIgnoreAttribute"))
                members = members.Add(member);
        }
        return members;
    }
}