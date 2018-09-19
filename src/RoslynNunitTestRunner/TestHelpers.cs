using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RoslynNunitTestRunner.Reflection;

namespace RoslynNunitTestRunner
{
    public sealed class MarkupCodeInvalidFormatException : FormatException
    {
        public MarkupCodeInvalidFormatException(string message) : base(message)
        {}
    }

    public class ProcessedCode
    {
        public ProcessedCode(string code, List<TextSpan> spans)
        {
            Contract.Requires(code != null);
            Contract.Requires(spans != null);
            Code = code;
            Spans = spans;
        }

        public string Code { get; }
        public List<TextSpan> Spans { get; }
        public Document Document { get; private set; }
        public string GetCodeWithMarkers(IEnumerable<TextSpan> spans) => WithInsertedDiagnostics(spans);

        public ProcessedCode WithDocument(Document document)
        {
            Document = document;
            return this;
        }

        [Pure]
        private string WithInsertedDiagnostics(IEnumerable<TextSpan> spans)
        {
            string result = Code;
            foreach (var span in spans.OrderByDescending(x => x.End))
            {
                result = result.Insert(span.End, "|]");
                result = result.Insert(span.Start, "[|");
            }

            return result;
        }
    }

    public static class TestHelpers
    {
        public const string StartMarker = "[|";
        public const string EndMarker = "|]";

        public static ProcessedCode ProcessMarkupCode(string markupCode)
        {
            var spans = new List<TextSpan>();

            var builder = new StringBuilder();
            int position = 0;
            while (position < markupCode.Length)
            {
                var start = markupCode.IndexOf(StartMarker, position);
                if (start == -1)
                {
                    // Marker was not found. Maybe it is missing in the markupCode or just missing for this iteration.
                    builder.Append(markupCode.Substring(position));
                    break;
                }

                // Adding "code " from "code [|" 
                builder.Append(markupCode.Substring(position, start - position));
                position = start + StartMarker.Length;

                var end = markupCode.IndexOf(EndMarker, position);
                if (end == -1)
                {
                    throw new MarkupCodeInvalidFormatException($"Can't find end marker ('{EndMarker}') in markup code.");
                }

                int markerSize = StartMarker.Length;

                // Adding "blah" from "code [|blah|]"
                builder.Append(markupCode.Substring(position, end - position));

                position = end + EndMarker.Length;

                spans.Add(TextSpan.FromBounds(start, end - markerSize));
            }

            // Need to adjust beginning of the span, because each previous marker affects next spans
            var correctedSpans = spans.Select((span, index) => new TextSpan(span.Start - (index*4), span.Length)).ToList();

            return new ProcessedCode(builder.ToString(), correctedSpans);
        }

        /// <summary>
        /// Converts specified <paramref name="markupCode"/> to regular C# code by removing markers.
        /// </summary>
        /// <exception cref="MarkupCodeInvalidFormatException">Throws if specified code does not have start or end markers.</exception>
        [Obsolete]
        public static bool TryGetCodeAndSpanFromMarkup(string markupCode, out string code, out TextSpan span)
        {
            code = null;
            span = default(TextSpan);

            var builder = new StringBuilder();

            var start = markupCode.IndexOf(StartMarker);
            if (start < 0)
            {
                throw new MarkupCodeInvalidFormatException($"Can't find start marker ('{StartMarker}') in markup code.");
            }

            builder.Append(markupCode.Substring(0, start));

            var end = markupCode.IndexOf(EndMarker);
            if (end < 0)
            {
                throw new MarkupCodeInvalidFormatException($"Can't find end marker ('{EndMarker}') in markup code.");
            }

            int markerSize = StartMarker.Length;
            builder.Append(markupCode.Substring(start + markerSize, end - start - markerSize));
            builder.Append(markupCode.Substring(end + markerSize));

            code = builder.ToString();
            span = TextSpan.FromBounds(start, end - markerSize);

            return true;
        }

        public static ProcessedCode GetDocumentAndSpansFromMarkup(string markupCode, string languageName, ImmutableList<MetadataReference> references = null)
        {
            var processed = ProcessMarkupCode(markupCode);
            var document = GetDocument(processed.Code, languageName, references);
            return processed.WithDocument(document);
        }

        [Obsolete]
        public static bool TryGetDocumentAndSpanFromMarkup(string markupCode, string languageName, out Document document, out TextSpan span)
        {
            return TryGetDocumentAndSpanFromMarkup(markupCode, languageName, null, out document, out span);
        }

        [Obsolete]
        public static bool TryGetDocumentAndSpanFromMarkup(string markupCode, string languageName, ImmutableList<MetadataReference> references, out Document document, out TextSpan span)
        {
            if (!TryGetCodeAndSpanFromMarkup(markupCode, out var code, out span))
            {
                document = null;
                return false;
            }

            document = GetDocument(code, languageName, references);
            return true;
        }

        public static Document GetDocument(string code, string languageName, ImmutableList<MetadataReference> references = null)
        {
            references = references ?? ImmutableList.Create<MetadataReference>(
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.GetLocation()),
                MetadataReference.CreateFromFile(typeof(Regex).GetTypeInfo().Assembly.GetLocation()),
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.GetLocation()));

            return new AdhocWorkspace()
                .AddProject("TestProject", languageName)
                .AddMetadataReferences(references)
                .AddDocument("TestDocument", code);
        }
    }
}
