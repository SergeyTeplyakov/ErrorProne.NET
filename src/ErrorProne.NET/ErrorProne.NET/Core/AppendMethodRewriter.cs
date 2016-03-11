using System.Diagnostics.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    /// <summary>
    /// Rewriter that adds extracted oldMethod to the class.
    /// </summary>
    internal class AppendMethodRewriter : CSharpSyntaxRewriter
    {
        private readonly MethodDeclarationSyntax _oldMethod;
        private readonly MethodDeclarationSyntax _newMethod;
        private readonly MethodDeclarationSyntax _extractedMethod;

        public AppendMethodRewriter(MethodDeclarationSyntax oldMethod, MethodDeclarationSyntax newMethod, MethodDeclarationSyntax extractedMethod)
            : base(visitIntoStructuredTrivia: false)
        {
            Contract.Requires(oldMethod != null);
            Contract.Requires(newMethod != null);
            Contract.Requires(extractedMethod != null);

            _oldMethod = oldMethod;
            _newMethod = newMethod;
            _extractedMethod = extractedMethod;
        }

        public static SyntaxNode AppendMethod(SyntaxNode root, 
            MethodDeclarationSyntax oldMethod, 
            MethodDeclarationSyntax newMethod, 
            MethodDeclarationSyntax extractedMethod)
        {
            var rewriter = new AppendMethodRewriter(oldMethod, newMethod, extractedMethod);
            return rewriter.Visit(root);
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var classDeclaration = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            
            // Need to add extracted oldMethod right after original one
            var members = classDeclaration.Members;
            
            int index = members.IndexOf(_oldMethod);
            members = members.Remove(_oldMethod).InsertRange(index, new[] {_newMethod, _extractedMethod});
            //members = index == -1 ? members.Add(_extractedMethod) : members.Insert(index, _extractedMethod);

            return classDeclaration.WithMembers(members);
        }
    }
}