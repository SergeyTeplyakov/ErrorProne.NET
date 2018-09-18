using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SwitchAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SwitchAnalyzer : DiagnosticAnalyzer
    {

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(EnumAnalyzer.Rule, InterfaceAnalyzer.Rule, ClassAnalyzer.Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockAction(Action);
        }

        private static void Action(CodeBlockAnalysisContext context)
        {
            var blockSyntaxes = context.CodeBlock.ChildNodes().OfType<BlockSyntax>(); ;

            var switchStatements = blockSyntaxes.SelectMany(x => x.Statements.OfType<SwitchStatementSyntax>());

            foreach (var switchStatement in switchStatements)
            {
                CheckSwitch(switchStatement, context);
            }
        }

        private static void CheckSwitch(SwitchStatementSyntax switchStatement, CodeBlockAnalysisContext context)
        {
            var expression = switchStatement.Expression;
            var typeInfo = context.SemanticModel.GetTypeInfo(expression);
            var expressionType = typeInfo.ConvertedType;
            var switchCases = switchStatement.Sections;
            var switchLocationStart = switchStatement.GetLocation().SourceSpan.Start;

            if (expressionType.TypeKind == TypeKind.Enum)
            {
                bool ShouldProceed() => EnumAnalyzer.ShouldProceedWithChecks(switchCases);
                IEnumerable<string> CaseImplementations() => EnumAnalyzer.CaseIdentifiers(switchCases);
                
                var namedType = expressionType as INamedTypeSymbol;
                switch (namedType.EnumUnderlyingType.Name)
                {
                    case "Int32":
                        {
                            IEnumerable<SwitchArgumentTypeItem<int>> AllImplementations() => EnumAnalyzer.AllEnumValues<int>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "UInt32":
                        {
                            IEnumerable<SwitchArgumentTypeItem<uint>> AllImplementations() => EnumAnalyzer.AllEnumValues<uint>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "Int64":
                        {
                            IEnumerable<SwitchArgumentTypeItem<long>> AllImplementations() => EnumAnalyzer.AllEnumValues<long>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "UInt64":
                        {
                            IEnumerable<SwitchArgumentTypeItem<ulong>> AllImplementations() => EnumAnalyzer.AllEnumValues<ulong>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "Byte":
                        {
                            IEnumerable<SwitchArgumentTypeItem<byte>> AllImplementations() => EnumAnalyzer.AllEnumValues<byte>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "SByte":
                        {
                            IEnumerable<SwitchArgumentTypeItem<sbyte>> AllImplementations() => EnumAnalyzer.AllEnumValues<sbyte>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "Int16":
                        {
                            IEnumerable<SwitchArgumentTypeItem<short>> AllImplementations() => EnumAnalyzer.AllEnumValues<short>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                    case "UInt16":
                        {
                            IEnumerable<SwitchArgumentTypeItem<ushort>> AllImplementations() => EnumAnalyzer.AllEnumValues<ushort>(expressionType);
                            ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, EnumAnalyzer.Rule);
                            break;
                        }
                }
            }

            if (expressionType.TypeKind == TypeKind.Interface)
            {
                bool ShouldProceed() => InterfaceAnalyzer.ShouldProceedWithChecks(switchCases);
                IEnumerable<SwitchArgumentTypeItem<string>> AllImplementations() => InterfaceAnalyzer.GetAllImplementationNames(switchLocationStart, expressionType, context.SemanticModel);
                IEnumerable<string> CaseImplementations() => PatternMatchingHelper.GetCaseValues(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, InterfaceAnalyzer.Rule);
            }

            if (expressionType.TypeKind == TypeKind.Class)
            {
                bool ShouldProceed() => ClassAnalyzer.ShouldProceedWithChecks(switchCases, expressionType.Name);
                IEnumerable<SwitchArgumentTypeItem<string>> AllImplementations() => ClassAnalyzer.GetAllImplementationNames(switchLocationStart, expressionType, context.SemanticModel);
                IEnumerable<string> CaseImplementations() => PatternMatchingHelper.GetCaseValues(switchCases);

                ProcessSwitch(ShouldProceed, AllImplementations, CaseImplementations, ClassAnalyzer.Rule);
            }

            void ProcessSwitch<T>(Func<bool> shouldProceedFunc,
                Func<IEnumerable<SwitchArgumentTypeItem<T>>> allImplementationsFunc,
                Func<IEnumerable<string>> caseImplementationFunc,
                DiagnosticDescriptor rule) where T: IComparable => ProcessSwitchCases(
                shouldProceedFunc: shouldProceedFunc,
                allImplementationsFunc: allImplementationsFunc,
                caseImplementationFunc: caseImplementationFunc,
                rule: rule,
                location: switchStatement.GetLocation(),
                context: context,
                switchStatementLocation: switchLocationStart);
        }

        private static void ProcessSwitchCases<T>(
            Func<bool> shouldProceedFunc, 
            Func<IEnumerable<SwitchArgumentTypeItem<T>>> allImplementationsFunc,
            Func<IEnumerable<string>> caseImplementationFunc,
            DiagnosticDescriptor rule,
            Location location,
            CodeBlockAnalysisContext context,
            int switchStatementLocation) where T: IComparable
        {
            if (shouldProceedFunc == null
                || allImplementationsFunc == null
                || caseImplementationFunc == null
                || rule == null)
                return;

            if (!shouldProceedFunc())
                return;

            var allImplementations = allImplementationsFunc().ToList();

            var obj = new object();
            var caseImplementations = caseImplementationFunc().ToDictionary(x => x, _ => obj);

            var checkedValues = allImplementations
                .Where(expectedValue => caseImplementations.ContainsKey(expectedValue.FullName))
                .ToDictionary(x => x.Value, x => obj);

            var notCheckedValues = allImplementations.Where(x =>
                !checkedValues.ContainsKey(x.Value))
                .OrderBy(x => x.FullName)
                .ToList();

            if (notCheckedValues.Any())
            {
                var firstUncheckedValue = notCheckedValues.First();
                var typeName = firstUncheckedValue.Member;
                var symbols = context.SemanticModel.LookupSymbols(switchStatementLocation);
                var shouldAddNamespace = !symbols.Any(x => x.Name == typeName && x.ContainingNamespace.Name == firstUncheckedValue.Prefix);

                var notCoveredValues = notCheckedValues.Select(caseName =>
                    BuildName(shouldAddNamespace, caseName.Prefix, caseName.FullName));
                var diagnostic = Diagnostic.Create(rule, location, string.Join(", ", notCoveredValues));
                context.ReportDiagnostic(diagnostic);
            }

            string BuildName(bool shouldAddPrefix, string prefix, string fullName) => shouldAddPrefix ? $"{prefix}.{fullName}" : fullName;
        }
    }


}
