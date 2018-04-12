using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RoslynNunitTestRunner
{
    public abstract class CodeRefactoringTestFixture : BaseTestFixture
    {
        protected abstract CodeRefactoringProvider CreateProvider();

        protected void TestCodeRefactoring(string markupCode, string expected)
        {
            Assert.That(TestHelpers.TryGetDocumentAndSpanFromMarkup(markupCode, LanguageName, out var document, out var span), Is.True);

            TestCodeRefactoring(document, span, expected);
        }

        protected void TestCodeRefactoring(Document document, TextSpan span, string expected)
        {
            var codeRefactorings = GetCodeRefactorings(document, span);

            Assert.That(codeRefactorings.Length, Is.EqualTo(1));

            Verify.CodeAction(codeRefactorings[0], document, expected);
        }

        private ImmutableArray<CodeAction> GetCodeRefactorings(Document document, TextSpan span)
        {
            var builder = ImmutableArray.CreateBuilder<CodeAction>();
            Action<CodeAction> registerRefactoring = a => builder.Add(a);

            var context = new CodeRefactoringContext(document, span, registerRefactoring, CancellationToken.None);
            var provider = CreateProvider();
            provider.ComputeRefactoringsAsync(context).Wait();

            return builder.ToImmutable();
        }
    }
}
