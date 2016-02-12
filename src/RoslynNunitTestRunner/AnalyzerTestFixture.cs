using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RoslynNunitTestRunner
{
    public abstract class CSharpAnalyzerTestFixture<T> : AnalyzerTestFixture where T : DiagnosticAnalyzer, new()
    {
        protected override string LanguageName => LanguageNames.CSharp;

        protected override DiagnosticAnalyzer CreateAnalyzer()
        {
            return new T();
        }
    }

    public abstract class AnalyzerTestFixture : BaseTestFixture
    {
        protected abstract DiagnosticAnalyzer CreateAnalyzer();

        protected void NoDiagnostic(string code, string diagnosticId)
        {
            var document = TestHelpers.GetDocument(code, LanguageName);

            NoDiagnostic(document, diagnosticId);
        }

        protected void NoDiagnostic(Document document, string diagnosticId)
        {
            var diagnostics = GetDiagnostics(document);

            Assert.That(diagnostics.Any(d => d.Id == diagnosticId), Is.False);
        }

        protected void HasDiagnostic(string markupCode, string diagnosticId)
        {
            Document document;
            TextSpan span;
            bool result = TestHelpers.TryGetDocumentAndSpanFromMarkup(markupCode, LanguageName, out document, out span);
            Assert.IsTrue(result, "Can't create document from specified markup code");

            HasDiagnostic(document, span, diagnosticId);
        }

        protected void HasDiagnostic(Document document, TextSpan span, string diagnosticId)
        {
            var diagnostics = GetDiagnostics(document);
            Assert.That(diagnostics.Length, Is.EqualTo(1), "Expected exactly one diagnostic");

            var diagnostic = diagnostics[0];
            Assert.That(diagnostic.Id, Is.EqualTo(diagnosticId));
            Assert.That(diagnostic.Location.IsInSource, Is.True);
            Assert.That(diagnostic.Location.SourceSpan, Is.EqualTo(span));
        }

        private ImmutableArray<Diagnostic> GetDiagnostics(Document document)
        {
            var analyzers = ImmutableArray.Create(CreateAnalyzer());
            var compilation = document.Project.GetCompilationAsync(CancellationToken.None).Result;
            
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, cancellationToken: CancellationToken.None);
            var discarded = compilation.GetDiagnostics(CancellationToken.None);

            var tree = document.GetSyntaxTreeAsync(CancellationToken.None).Result;

            var builder = ImmutableArray.CreateBuilder<Diagnostic>();
            
            foreach (var diagnostic in compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result)
            {
                var location = diagnostic.Location;
                if (location.IsInSource && location.SourceTree == tree)
                {
                    builder.Add(diagnostic);
                }
            }

            return builder.ToImmutable();
        }
    }
}
