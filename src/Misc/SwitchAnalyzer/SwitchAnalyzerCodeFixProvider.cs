using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SwitchAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchAnalyzerCodeFixProvider)), Shared]
    public class SwitchAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add missing cases";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            EnumAnalyzer.DiagnosticId, 
            InterfaceAnalyzer.DiagnosticId,
            ClassAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var message = diagnostic.GetMessage();
            var caseNames = GetMissingCases(message);

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedSolution: c => AddEmptyCases(root, context.Document, diagnosticSpan, caseNames, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Solution> AddEmptyCases(SyntaxNode root, Document document, TextSpan diagnosticSpan,
            IEnumerable<string> caseNames, CancellationToken cancellationToken)
        {
            var switchToken = root.FindToken(diagnosticSpan.Start).Parent as SwitchStatementSyntax;
            if (switchToken == null)
            {
                return document.Project.Solution;
            }

            var missingLabels = caseNames.Select(CreatEmptyNode).ToList();

            var (section, created) = GetOrCreateStatement(switchToken);

            var newSectionLabels = section.Labels.InsertRange(0, missingLabels);
            var newSection = section.WithLabels(newSectionLabels);
            if (created)
            {
                var breakStatement = Block(
                    SingletonList<StatementSyntax>(
                        BreakStatement()));
                newSection = newSection.WithStatements(SingletonList<StatementSyntax>(breakStatement));
            }

            var newSections = ApplySection(switchToken, section, created, newSection);

            var newSwitch = switchToken.WithSections(newSections);

            var documentEditor = await DocumentEditor.CreateAsync(document, cancellationToken);
            documentEditor.ReplaceNode(switchToken, newSwitch);

            var newDocument = documentEditor.GetChangedDocument();

            return newDocument.Project.Solution;
        }

        private static SyntaxList<SwitchSectionSyntax> ApplySection(SwitchStatementSyntax switchToken, SwitchSectionSyntax section, bool created, SwitchSectionSyntax newSection)
        {
            SyntaxList<SwitchSectionSyntax> newSections;
            newSections = created
                ? switchToken.Sections.Add(newSection)
                : switchToken.Sections.Replace(section, newSection);

            return newSections;
        }

        private (SwitchSectionSyntax section, bool created) GetOrCreateStatement(SwitchStatementSyntax swtichStatement)
        {
            var defaultSection =
                swtichStatement.Sections.FirstOrDefault(x => x.Labels.Any(label => label is DefaultSwitchLabelSyntax));

            return defaultSection != null 
                ? (defaultSection, created: false) 
                : (SwitchSection(), created: true);
        }

        private SwitchLabelSyntax CreatEmptyNode(string caseName)
        {
            var parts = caseName.Split('.');

            var colonToken = Token(SyntaxKind.ColonToken);

            // Namespace or other prefix
            if (parts.Length == 3)
            {
                var identifier =
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(parts[0]),
                            IdentifierName(parts[1])),
                        IdentifierName(parts[2]));
                return CaseSwitchLabel(identifier, colonToken);
            }

            // Enum value (enum + case)
            if (parts.Length == 2)
            {
                var identifier = 
                 MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(parts.First()),
                    IdentifierName(parts.Last()));
                return CaseSwitchLabel(identifier, colonToken);
            }

            // Class/interface implementation
            if (parts.Length == 1)
            {
                var declaration = DeclarationPattern(
                    ParseTypeName(caseName), DiscardDesignation());
                return CasePatternSwitchLabel(declaration, colonToken);
            }

            throw new NotImplementedException($"Unknown pattern for '{caseName}'");
        }

        private IEnumerable<string> GetMissingCases(string message)
        {
            var parts = message.Split(new[] {": "}, StringSplitOptions.RemoveEmptyEntries);
            var casesPart = parts.Last();
            return casesPart.Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
