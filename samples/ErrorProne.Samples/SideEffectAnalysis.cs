using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Linq;

namespace ErrorProne.Samples
{
    public class SideEffectAnalysis
    {
        [Pure]
        public static string PureMethod() { return "42"; }

        public void NonObservedReturnValue()
        {
            // Non-observed return value of the pure method

            // Linq methods
            Enumerable.Range(1, 10);
            Enumerable.Range(1, 5).Select(x => x.ToString()).FirstOrDefault();

            // Third-party extensions methods for IEnumerable<T>
            new int[] { 1, 2 }.ToImmutableList(); // Non-Observed return value

            // Methods on string
            "x".Substring(1); // Non-Observed return value

            // On pure methods
            PureMethod(); // Non-Observed return value

            // member of all immutable collections
            var list = Enumerable.Range(1, 10).ToImmutableList();
            list.Add(42); // Non-Observed return value

            // On With pattern
            var tree = CSharpSyntaxTree.ParseText("class Foo {}");
            tree.WithFilePath("path"); // Non-Observed return value

            // Calls to well-known system types
            Convert.ToByte(42); // Non-Observed return value
            ToString(); // Non-Observed return value
            object.ReferenceEquals(null, 42); // Non-Observed return value
            IComparable<int> n = 42; 
            n.CompareTo(3); // Non-Observed return value

            // Static methods on well-known structs
            char.IsDigit('c');
            int.Parse("foo");
        }

        enum CustomEnum { }
        struct CustomStruct { }
        public void AssignmentFreeObjectConstruction()
        {
            // Warning on constructor call that is known to be side-effect free

            // Assignment free construction of well-known primitive types
            new object();
            new string('c', 42);

            // default constructor for structs
            new int();
            // Including custom structs
            new CustomStruct();

            // Default constructor for enums
            new CustomEnum();

            // Default constructor for all collections
            new List<int>();
        }

        public void SideEffectFreeExceptionConstruction()
        {
            // Special rule for side effect free exception construction
            // This is considered an error!
            //new Exception();
        }
    }
}