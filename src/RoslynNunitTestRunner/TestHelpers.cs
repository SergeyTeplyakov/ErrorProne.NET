using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
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

    public static class TestHelpers
    {
        public const string StartMarker = "[|";
        public const string EndMarker = "|]";

        /// <summary>
        /// Converts specified <paramref name="markupCode"/> to regular C# code by removing markers.
        /// </summary>
        /// <exception cref="MarkupCodeInvalidFormatException">Throws if specified code does not have start or end markers.</exception>
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

        public static bool TryGetDocumentAndSpanFromMarkup(string markupCode, string languageName, out Document document, out TextSpan span)
        {
            return TryGetDocumentAndSpanFromMarkup(markupCode, languageName, null, out document, out span);
        }

        public static bool TryGetDocumentAndSpanFromMarkup(string markupCode, string languageName, ImmutableList<MetadataReference> references, out Document document, out TextSpan span)
        {
            string code;
            if (!TryGetCodeAndSpanFromMarkup(markupCode, out code, out span))
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
                MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.GetLocation()));

            return new AdhocWorkspace()
                .AddProject("TestProject", languageName)
                .AddMetadataReferences(references)
                .AddDocument("TestDocument", code);
        }
    }
}
