using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RoslynNunitTestRunner
{
    public abstract class CSharpCodeFixTestFixture<T> : CodeFixTestFixture where T : CodeFixProvider, new()
    {
        protected override string LanguageName => LanguageNames.CSharp;

        protected override CodeFixProvider CreateProvider()
        {
            return new T();
        }
    }

    public abstract class CodeFixTestFixture : BaseTestFixture
    {
        protected abstract CodeFixProvider CreateProvider();

        protected void TestCodeFix(string markupCode, string expected, DiagnosticDescriptor descriptor, params object[] additionalArgs)
        {
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(markupCode, LanguageName);

            TestCodeFix(processedDocument.Document, processedDocument.Spans.First(), expected, descriptor, additionalArgs);
        }

        protected void TestNoCodeFix(string markupCode, DiagnosticDescriptor descriptor)
        {
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(markupCode, LanguageName);

            TestNoCodeFix(processedDocument.Document, processedDocument.Spans.First(), descriptor);
        }

        protected void TestNoCodeFix(Document document, TextSpan span, DiagnosticDescriptor descriptor)
        {
            var codeFixes = GetCodeFixes(document, span, descriptor);
            Assert.That(codeFixes.Length, Is.EqualTo(0), "Fixer should not be available");
        }

        protected void TestCodeFix(Document document, TextSpan span, string expected, DiagnosticDescriptor descriptor, params object[] additionalArgs)
        {
            var codeFixes = GetCodeFixes(document, span, descriptor, additionalArgs);
            Assert.That(codeFixes.Length, Is.EqualTo(1));

            Verify.CodeAction(codeFixes[0], document, expected);
        }

        private ImmutableArray<CodeAction> GetCodeFixes(Document document, TextSpan span, DiagnosticDescriptor descriptor, params object[] additionalArgs)
        {
            var builder = ImmutableArray.CreateBuilder<CodeAction>();
            Action<CodeAction, ImmutableArray<Diagnostic>> registerCodeFix =
                (a, _) => builder.Add(a);

            var tree = document.GetSyntaxTreeAsync(CancellationToken.None).Result;
            var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, span), additionalArgs);
            var context = new CodeFixContext(document, diagnostic, registerCodeFix, CancellationToken.None);

            var provider = CreateProvider();
            provider.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();

            return builder.ToImmutable();
        }
    }
}
