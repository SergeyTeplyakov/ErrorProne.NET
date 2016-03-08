using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace ErrorProne.NET.Refactorings
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp), Shared]
    public sealed class ConvertToReadOnlyAttribute : CodeRefactoringProvider
    {
        public const string RefactoringId = "ConvertToReadOnlyAttribute";

        public override Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}