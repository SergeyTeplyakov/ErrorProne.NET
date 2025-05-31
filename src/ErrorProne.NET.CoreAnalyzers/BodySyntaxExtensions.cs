﻿using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    public static class BodySyntaxExtensions
    {
        public static MethodDeclarationSyntax? WithStatements(this MethodDeclarationSyntax method,
            IEnumerable<StatementSyntax> statements)
        {
            if (method.Body == null)
            {
                return null;
            }

            return method.WithBody(method.Body.WithStatements(new SyntaxList<StatementSyntax>().AddRange(statements)));
        }
    }
}