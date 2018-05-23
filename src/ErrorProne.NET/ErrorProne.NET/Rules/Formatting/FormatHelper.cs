using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using ErrorProne.NET.Common;
using ErrorProne.NET.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Rules.Formatting
{
    internal sealed class ParseResult
    {
        public bool IsValid;
        public HashSet<int> UsedIndices;

        public ArgumentSyntax FormatArgument;
        public string Format;
        public ExpressionSyntax[] Args;
    }

    internal static class FormatHelper
    {
        const string expectedFormatArgumentName = "format";

        private static Dictionary<ITypeSymbol, string> _formattableMembersByNamedTypes;
        private static Dictionary<string, string> _formattableMembersByFullName;

        private static Dictionary<ITypeSymbol, string> GetFormattableMembers(SemanticModel model)
        {
            return new Dictionary<ITypeSymbol, string>()
            {
                [model.GetClrType(typeof(string))] = nameof(string.Format),
                [model.GetClrType(typeof(StringBuilder))] = nameof(StringBuilder.AppendFormat),
            };
        }

        private static Dictionary<string, string> GetFormattableMembersByFullName()
        {
            return new Dictionary<string, string>()
            {
                ["System.Console"] = "WriteLine",
            };
        }

        public static bool IsFormattableCall(IMethodSymbol method, SemanticModel semanticModel)
        {
            _formattableMembersByNamedTypes = GetFormattableMembers(semanticModel);
            LazyInitializer.EnsureInitialized(ref _formattableMembersByFullName, () => GetFormattableMembersByFullName());

            // Some well-known types we know upfront
            string methodName;
            if (_formattableMembersByNamedTypes.TryGetValue(method.ReceiverType, out methodName))
            {
                if (method.Name == methodName)
                {
                    return true;
                }
            }

            // But some types we can't reference from Portable library, for instance, System.Console
            if (_formattableMembersByFullName.TryGetValue(method.ReceiverType.FullName(), out methodName))
            {
                // Valid only if method has 'format' argument! 
                // TODO: need to extend the dictionary and provide format argument name!
                if (method.Name == methodName && method.Parameters.Any(p => p.Name == expectedFormatArgumentName))
                {
                    return true;
                }
            }

            if (MarkedWithStringFormatMethodAttribute(method))
            {
                return true;
            }

            return false;
        }

        private static bool MarkedWithStringFormatMethodAttribute(IMethodSymbol method)
        {
            return 
                method.GetAttributes()
                .Select(a => a.AttributeClass.FullName())
                .Any(a => a == "JetBrains.Annotations.StringFormatMethodAttribute");
        }

        // TODO: analyze not only with format litteral but with method calls that returns formatted literal!
        private static string GetFormatArgumentName(IMethodSymbol symbol)
        {
            var annotationAttribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass.FullName() == "JetBrains.Annotations.StringFormatMethodAttribute");
            if (annotationAttribute != null)
            {
                var firstArgument = annotationAttribute.ConstructorArguments.FirstOrDefault();
                if (firstArgument.Value != null)
                {
                    return firstArgument.Value.ToString();
                }
            }

            return expectedFormatArgumentName;
        }

        private static Tuple<ArgumentSyntax, ExpressionSyntax[]> ExtractFormatArgumentAndArgs(InvocationExpressionSyntax invocation, IMethodSymbol symbol, SemanticModel semanticModel)
        {
            Contract.Ensures(Contract.Result<Tuple<ArgumentSyntax, ExpressionSyntax[]>>() != null);
            Contract.Ensures(Contract.Result<Tuple<ArgumentSyntax, ExpressionSyntax[]>>().Item1 != null);

            // Current implementation is based on postitional arguments!
            // TODO: Add implemnetation that will work properly with named arguments as well!

            var formatArgumentName = GetFormatArgumentName(symbol);

            int formatIndex = 0;
            ArgumentSyntax formatArgument = null;

            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                if (symbol.Parameters[i].Name == formatArgumentName)
                {
                    formatIndex = i;
                    formatArgument = invocation.ArgumentList.Arguments[i];
                }
            }

            var rest = invocation.ArgumentList.Arguments.Skip(formatIndex + 1).Select(x => x.Expression).ToArray();

            if (rest.Length == 1)
            {
                var arrayCreation = rest[0] as ArrayCreationExpressionSyntax;
                if (arrayCreation != null)
                {
                    rest = arrayCreation.Initializer.Expressions.Select(x => x).ToArray();
                    //rest = arrayCreation.Initializer
                }
                // This could be an array creation expression!
                // If so, then expand the expression!
            }

            return Tuple.Create(formatArgument, rest);
        }
        
        public static ParseResult ParseFormatMethodInvocation(InvocationExpressionSyntax invocation,
            IMethodSymbol symbol, SemanticModel semanticModel)
        {
            Tuple<ArgumentSyntax, ExpressionSyntax[]> arguments = ExtractFormatArgumentAndArgs(invocation, symbol,
                semanticModel);

            var format = arguments.Item1.Expression.GetLiteral(semanticModel);

            HashSet<int> usedIndices = null;
            bool isValid = string.IsNullOrEmpty(format) || ParseFormatString(format, out usedIndices);
            
            return new ParseResult()
            {
                IsValid = isValid,
                UsedIndices = usedIndices ?? new HashSet<int>(),
                Format = format,
                FormatArgument = arguments.Item1,
                Args = arguments.Item2
            };
        }

        private static bool ParseFormatString(string format, out HashSet<int> usedIndices)
        {
            //Contract.Ensures(Contract.Result<StringBuilder>() != null);
            //Contract.EndContractBlock();
            usedIndices = new HashSet<int>();

            int pos = 0;
            int len = format.Length;
            char ch = '\x0';

            while (true)
            {
                int p = pos;
                int i = pos;
                while (pos < len)
                {
                    ch = format[pos];

                    pos++;
                    if (ch == '}')
                    {
                        if (pos < len && format[pos] == '}') // Treat as escape character for }}
                            pos++;
                        else
                            return false;
                    }

                    if (ch == '{')
                    {
                        if (pos < len && format[pos] == '{') // Treat as escape character for {{
                            pos++;
                        else
                        {
                            pos--;
                            break;
                        }
                    }

                    //Append(ch);
                }

                if (pos == len) break;
                pos++;
                if (pos == len || (ch = format[pos]) < '0' || ch > '9') return false;//FormatError();
                int index = 0;
                do
                {
                    index = index * 10 + ch - '0';
                    pos++;
                    if (pos == len) return false;//FormatError();
                    ch = format[pos];
                } while (ch >= '0' && ch <= '9' && index < 1000000);

                usedIndices.Add(index);
                //if (index >= args.Length) //throw new FormatException(Environment.GetResourceString("Format_IndexOutOfRange"));

                while (pos < len && (ch = format[pos]) == ' ') pos++;
                int width = 0;
                if (ch == ',')
                {
                    pos++;
                    while (pos < len && format[pos] == ' ') pos++;

                    if (pos == len) return false;//FormatError();
                    ch = format[pos];
                    if (ch == '-')
                    {
                        pos++;
                        if (pos == len) return false;// FormatError();
                        ch = format[pos];
                    }
                    if (ch < '0' || ch > '9') return false; //FormatError();
                    do
                    {
                        width = width * 10 + ch - '0';
                        pos++;
                        if (pos == len) return false; //FormatError();
                        ch = format[pos];
                    } while (ch >= '0' && ch <= '9' && width < 1000000);
                }

                while (pos < len && (ch = format[pos]) == ' ') pos++;
                
                //Object arg = args[index];
                StringBuilder fmt = null;
                if (ch == ':')
                {
                    pos++;
                    p = pos;
                    i = pos;
                    while (true)
                    {
                        if (pos == len) return false; // FormatError();
                        ch = format[pos];
                        pos++;
                        if (ch == '{')
                        {
                            if (pos < len && format[pos] == '{')  // Treat as escape character for {{
                                pos++;
                            else
                                return false;// FormatError();
                        }
                        else if (ch == '}')
                        {
                            if (pos < len && format[pos] == '}')  // Treat as escape character for }}
                                pos++;
                            else
                            {
                                pos--;
                                break;
                            }
                        }

                        if (fmt == null)
                        {
                            fmt = new StringBuilder();
                        }
                        fmt.Append(ch);
                    }
                }

                if (ch != '}') return false; //FormatError();
                pos++;
            }

            return true;
        }
    }
}