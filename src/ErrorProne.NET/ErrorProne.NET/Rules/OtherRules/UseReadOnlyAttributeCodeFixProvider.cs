using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using ErrorProne.NET.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ErrorProne.NET.Rules.OtherRules
{
    [ExportCodeFixProvider("UseReadOnlyAttributeCodeFixProvider", LanguageNames.CSharp), Shared]
    public sealed class UseReadOnlyAttributeCodeFixProvider : CodeFixProvider
    {
        private const string FixText = "Use `ReadOnlyAttribute`";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RuleIds.UseReadOnlyAttributeInstead);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var node = root.FindNode(diagnosticSpan);
            var token = node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().First();

            if (token == null)
            {
                return;
            }

            // Removing readonly from existing modifiers
            var modifiers = token.Modifiers.Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword)).ToList();
            var nonReadonlyToken = token.WithModifiers(new SyntaxTokenList().AddRange(modifiers));

            nonReadonlyToken = nonReadonlyToken.WithAttributeLists(
                token.AttributeLists.Add(
                    AttributeList().AddAttributes(Attribute(
                        ParseName("ErrorProne.NET.Annotations.ReadOnly")))));

            var newRoot = root.ReplaceNode(token, nonReadonlyToken);
            
            var codeAction = CodeAction.Create(FixText, ct => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)));
            context.RegisterCodeFix(codeAction, diagnostic);
        }
    }
}