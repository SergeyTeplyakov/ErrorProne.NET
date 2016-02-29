using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorProne.Samples
{
    struct Immutable
    {
        private string s;

        public void PrintToConsole()
        {
            //Enumerable.Range(1, 10).Any(a => a == 4);
        }
    }

    class Foo
    {
        public readonly Immutable m;
        private readonly int n;
        public static void Test()
        {
            var foo = new Foo();
            foo.m.PrintToConsole();
            var r = foo.n.CompareTo(42);
        }
    }

    /// <summary>
    /// Semantic information associated with a query clause in a C# query expression.
    /// </summary>
    public struct QueryClauseInfo : IEquatable<QueryClauseInfo>
    {
        private readonly SymbolInfo _castInfo;
        private readonly SymbolInfo _operationInfo;

        internal QueryClauseInfo(SymbolInfo castInfo, SymbolInfo operationInfo)
        {
            _castInfo = castInfo;
            _operationInfo = operationInfo;
        }

        /// <summary>
        /// The .Cast&lt;T&gt;() operation generated from the query range variable's type restriction,
        /// or null if the type restriction isn't specified. 
        /// </summary>
        /// <remarks>
        /// The operation, when present is implemented via <see cref="IMethodSymbol"/>.
        /// To access the type, when this is non-null use <see cref="IMethodSymbol.TypeArguments"/>[0].
        /// If it is an extension method, it is returned in reduced form.
        /// </remarks>
        public SymbolInfo CastInfo
        {
            get { return _castInfo; }
        }

        /// <summary>
        /// The operation (e.g. Select(), Where(), etc) that implements the given clause.
        /// </summary>
        /// <remarks>
        /// The clause can be implemented via <see cref="IMethodSymbol"/>, or 
        /// <see cref="IFieldSymbol"/> or <see cref="IPropertySymbol"/> that return a delegate.
        /// If it is an extension method, it is returned in reduced form.
        /// </remarks>
        public SymbolInfo OperationInfo
        {
            get { return _operationInfo; }
        }

        public override bool Equals(object obj)
        {
            return obj is QueryClauseInfo && Equals((QueryClauseInfo)obj);
        }

        public bool Equals(QueryClauseInfo other)
        {
            return _castInfo.Equals(other._castInfo)
                && _operationInfo.Equals(other._operationInfo);
        }

        //public override int GetHashCode()
        //{
        //    return Hash.Combine(this.CastInfo.GetHashCode(), _operationInfo.GetHashCode());
        //}
    }
    class NonPureMethodOnReadonlyStructs
    {
    }
}
