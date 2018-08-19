using System;
using System.Collections.Generic;
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
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(code, LanguageName);
            Assert.That(processedDocument.Spans.Count, Is.EqualTo(0), "Document without diagnostics should not have [| |] marker.");
            NoDiagnostic(processedDocument.Document, diagnosticId, processedDocument);
        }

        protected void NoDiagnostic(
            Document document, string diagnosticId, ProcessedCode processedDocument)
        {
            var diagnostics = GetDiagnostics(document);
    
            string diagnosticMessage = string.Join("\r\n", diagnostics.Select(d => d.ToString()));
            Assert.That(diagnostics.Count(d => d.Id == diagnosticId), 
                Is.EqualTo(0), $"Expected no diagnostics, but got some:\r\n{diagnosticMessage}");
        }

        protected void HasDiagnostic(string markupCode, string diagnosticId)
        {
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(markupCode, LanguageName);

            HasDiagnostics(processedDocument, diagnosticId);
        }

        protected void HasDiagnostics(string markupCode, string[] diagnosticId)
        {
            var processedDocument = TestHelpers.GetDocumentAndSpansFromMarkup(markupCode, LanguageName);

            HasDiagnostics(processedDocument, diagnosticId);
        }

        protected void HasDiagnostics(ProcessedCode processed, string[] diagnosticId)
        {
            var document = processed.Document;
            var spans = processed.Spans;

            Assert.That(spans.Count, Is.GreaterThan(0), "At least one marker '[| |]' should be provided.");

            var diagnostics = GetDiagnostics(document);

            Console.WriteLine($"Got {diagnostics.Length} diagnostics:");
            Console.WriteLine(string.Join("\r\n", diagnostics));

            string expected = processed.GetCodeWithMarkers(diagnostics.Select(d => d.Location.SourceSpan).ToList());

            int expectedNumberOfDiagnostics = Math.Max(spans.Count, diagnosticId.Length);
            var message = $"Expected {expectedNumberOfDiagnostics} diagnostic(s). Document with diagnostics:\r\n{expected}";
            
            Assert.That(diagnostics.Length, Is.EqualTo(expectedNumberOfDiagnostics), message);

            var spanSet = new HashSet<TextSpan>(spans);

            var diagnosticsSet = new HashSet<string>(diagnosticId);

            foreach (var diagnostic in diagnostics)
            {
                Assert.True(diagnosticsSet.Contains(diagnostic.Id), 
                    $"Diagnostic '{diagnostic.Id}' is unknown. Known diagnostics: {string.Join(", ", diagnosticId)}");
                Assert.IsTrue(diagnostic.Location.IsInSource);
                Assert.IsTrue(spanSet.Contains(diagnostic.Location.SourceSpan), $"Can't find expected error. Expected:\r\n{expected}");
            }
        }

        protected void HasDiagnostics(ProcessedCode processed, string diagnosticId)
        {
            HasDiagnostics(processed, new []{diagnosticId});
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
