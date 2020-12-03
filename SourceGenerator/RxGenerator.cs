using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RxSourceGenerator
{
    [Generator]
    public class RxGenerator : ISourceGenerator
    {
        const string startText = "using System;using System.Reactive.Linq;namespace RxMethodGenerator{public static class RxGeneratedMethods{}}";
        private static readonly SymbolDisplayFormat SymbolDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                             SymbolDisplayGenericsOptions.IncludeVariance);

        public void Initialize(GeneratorInitializationContext context)
        {
#if (DEBUG)
        //    Debugger.Launch();
#endif
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) return;

            if (!(receiver.GenerateCandidates.Any()))
            {
                context.AddSource("RxGenerator.cs", startText);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Reactive.Linq;");
            sb.AppendLine("namespace RxMethodGenerator{");
            sb.AppendLine("    public static class RxGeneratedMethods{");

            foreach ((string classType, string eventName, string eventType, List<string> argumentTypes, bool isStub) in GetExtensionMethodInfo(context,
                receiver))
            {
                string tupleTypeStr;
                string conversionStr;

                switch (argumentTypes.Count)
                {
                    case 0:
                        tupleTypeStr = classType;
                        conversionStr = "conversion => () => conversion(obj),";
                        break;
                    case 1:
                        tupleTypeStr = argumentTypes.First();
                        conversionStr = "conversion => obj1 => conversion(obj1),";
                        break;
                    default:
                        tupleTypeStr =
                            $"({string.Join(", ", argumentTypes.Select((x, i) => $"{x} Item{i + 1}{x.Split('.').Last()}"))})";
                        string objStr = string.Join(", ", argumentTypes.Select((x, i) => $"obj{i}"));
                        conversionStr = $"conversion => ({objStr}) => conversion(({objStr})),";
                        break;
                }

                sb.AppendLine(
                    @$"        public static IObservable<{tupleTypeStr}> Rx{eventName}(this {classType} obj)");
                sb.AppendLine(@"        {");
                if (isStub)
                {
                    sb.AppendLine("            throw new Exception('RxGenerator stub');");
                }
                else
                {
                    sb.AppendLine("            if (obj == null) throw new ArgumentNullException(nameof(obj));");
                    sb.AppendLine(@$"            return Observable.FromEvent<{eventType}, {tupleTypeStr}>(");
                    sb.AppendLine(@$"            {conversionStr}");
                    sb.AppendLine(@$"            h => obj.{eventName} += h,");
                    sb.AppendLine(@$"            h => obj.{eventName} -= h);");
                }
                sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("RxGenerator.cs", sb.ToString());
        }

        private static IEnumerable<(string ClassType, string EventName, string EventType, List<string> ArgumentTypes, bool IsStub)>
            GetExtensionMethodInfo(GeneratorExecutionContext context, SyntaxReceiver receiver)
        {
            HashSet<(string ClassType, string EventName)>
                hashSet = new HashSet<(string ClassType, string EventName)>();
            foreach (MemberAccessExpressionSyntax syntax in receiver.GenerateCandidates)
            {
                SemanticModel model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                ITypeSymbol? typeSymbol = model.GetSymbolInfo(syntax.Expression).Symbol switch
                {
                    IMethodSymbol s => s.ReturnType,
                    ILocalSymbol s => s.Type,
                    IPropertySymbol s => s.Type,
                    IFieldSymbol s => s.Type,
                    IParameterSymbol s => s.Type,
                    _ => null
                };
                if (typeSymbol == null) continue;
                string eventName = syntax.Name.ToString().Substring(2);

                if (!(typeSymbol.GetMembersOfType<IEventSymbol>().FirstOrDefault(m => m.Name == eventName) is { } ev)
                ) continue;

                if (!(ev.Type is INamedTypeSymbol namedTypeSymbol)) continue;
                if (namedTypeSymbol.DelegateInvokeMethod == null) continue;
                if (!hashSet.Add((typeSymbol.ToString(), ev.Name))) continue;

                string fullType = namedTypeSymbol.ToDisplayString(SymbolDisplayFormat);
                List<string> typeArguments = namedTypeSymbol.DelegateInvokeMethod.Parameters
                    .Select(m => m.Type.ToDisplayString(SymbolDisplayFormat)).ToList();
                yield return (typeSymbol.ToString(), ev.Name, fullType, typeArguments, false);
            }
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<MemberAccessExpressionSyntax> GenerateCandidates { get; } =
                new List<MemberAccessExpressionSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (!(syntaxNode is MemberAccessExpressionSyntax syntax)) return;
                if (syntax.HasTrailingTrivia || syntax.Name.IsMissing) return;
                if (!syntax.Name.ToString().StartsWith("Rx")) return;
                GenerateCandidates.Add(syntax);

            }
        }
    }
}