using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
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
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(markupCode, LanguageName);

            HasDiagnostics(processedDocument, diagnosticId);
        }

        protected void HasDiagnostics(ProcessedCode processed, string diagnosticId)
        {
            var document = processed.Document;
            var spans = processed.Spans;

            var diagnostics = GetDiagnostics(document);
            string expected = processed.GetCodeWithMarkers(diagnostics.Select(d => d.Location.SourceSpan).ToList());

            var message = $"Expected {spans.Count} diagnostic(s). Document with diagnostics:\r\n{expected}";
            Assert.That(diagnostics.Length, Is.EqualTo(spans.Count), message);

            var spanSet = new HashSet<TextSpan>(spans);

            foreach (var diagnostic in diagnostics)
            {
                Assert.That(diagnostic.Id, Is.EqualTo(diagnosticId));
                Assert.IsTrue(diagnostic.Location.IsInSource);
                Assert.IsTrue(spanSet.Contains(diagnostic.Location.SourceSpan), $"{expected}");
            }
        }

        protected void HasDiagnostic(Document document, TextSpan span, string diagnosticId)
        {
            var diagnostics = GetDiagnostics(document);
            Assert.That(diagnostics.Length, Is.EqualTo(1), "Expected exactly one diagnostic");

            var diagnostic = diagnostics[0];
            Assert.That(diagnostic.Id, Is.EqualTo(diagnosticId));
            Assert.IsTrue(diagnostic.Location.IsInSource);

            
            string expected = document.GetSyntaxRootAsync().GetAwaiter().GetResult().ToString();

            expected = expected.Insert(span.End, "|]");
            expected = expected.Insert(span.Start, "[|");

            Assert.That(diagnostic.Location.SourceSpan, Is.EqualTo(span), $"{expected}");
        }

        private ImmutableArray<Diagnostic> GetDiagnostics(Document document)
        {
            var analyzers = ImmutableArray.Create(CreateAnalyzer());
            var compilation = document.Project.GetCompilationAsync(CancellationToken.None).Result;

            List<Exception> exceptions = new List<Exception>();

            var options = new CompilationWithAnalyzersOptions(
                options: new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: (exception, analyzer, arg3) => exceptions.Add(exception), 
                concurrentAnalysis: true, 
                logAnalyzerExecutionTime: false);
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, options);

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

            if (exceptions.Count != 0)
            {
                Assert.Fail($"Unhandled exception occurred during analysis: \r\n{string.Join("\r\n", exceptions)}");
            }

            return builder.ToImmutable();
        }
    }
}
