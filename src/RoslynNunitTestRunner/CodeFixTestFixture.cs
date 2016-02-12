using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RoslynNunitTestRunner
{
    public abstract class CodeFixTestFixture : BaseTestFixture
    {
        protected abstract CodeFixProvider CreateProvider();

        protected void TestCodeFix(string markupCode, string expected, DiagnosticDescriptor descriptor)
        {
            Document document;
            TextSpan span;
            Assert.IsTrue(TestHelpers.TryGetDocumentAndSpanFromMarkup(markupCode, LanguageName, out document, out span));

            TestCodeFix(document, span, expected, descriptor);
        }

        protected void TestCodeFix(Document document, TextSpan span, string expected, DiagnosticDescriptor descriptor)
        {
            var codeFixes = GetCodeFixes(document, span, descriptor);
            Assert.That(codeFixes.Length, Is.EqualTo(1));

            Verify.CodeAction(codeFixes[0], document, expected);
        }

        private ImmutableArray<CodeAction> GetCodeFixes(Document document, TextSpan span, DiagnosticDescriptor descriptor)
        {
            var builder = ImmutableArray.CreateBuilder<CodeAction>();
            Action<CodeAction, ImmutableArray<Diagnostic>> registerCodeFix =
                (a, _) => builder.Add(a);

            var tree = document.GetSyntaxTreeAsync(CancellationToken.None).Result;
            var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, span));
            var context = new CodeFixContext(document, diagnostic, registerCodeFix, CancellationToken.None);

            var provider = CreateProvider();
            provider.RegisterCodeFixesAsync(context).Wait();

            return builder.ToImmutable();
        }
    }
}
