using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileSystemGlobbing;

namespace roslynplay
{
    /// <summary>
    /// I made this before discovering SymbolFinder et al (see CallTracer).
    /// It tries to do what CallTracer does, but does it very badly.
    /// Maybe there's some Roslyn learnings I can salvage from this...
    /// </summary>
    internal class RoslynCallTracerExperiment
    {
        private static void RunFromArgs(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: dotnet run <project root dir>");
                return;
            }

            var rootDir = args[0];
            var methodName = args[1];

            Console.WriteLine("Building call graph...");
            var graph = BuildCallGraph(Glob(rootDir, "**/*.cs"));

            TraceCall(graph, new HashSet<string>(), methodName);
        }

        private static void TraceCall(
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            string methodName,
            int depth = 0)
        {
            if (depth > 20) throw new InvalidOperationException("too deep!");

            var calls = graph.Keys.Where(x => x.Contains(methodName));

            foreach (var call in calls)
            {
                var fqCallers = graph[call];
                var indent = new string(' ', depth * 2);
                Console.WriteLine($"{indent}{call}");
                foreach (var fqCaller in fqCallers)
                {
                    var callerMethodName = fqCaller.Split('.').Last();
                    if (!visited.Contains(fqCaller))
                        TraceCall(graph, visited, callerMethodName, depth + 1);
                    visited.Add(fqCaller);
                }
            }
        }

        private static IEnumerable<string> Glob(string rootDir, params string[] patterns)
        {
            var matcher = new Matcher();
            matcher.AddIncludePatterns(patterns);
            return matcher.GetResultsInFullPath(rootDir);
        }

        /// <summary>
        /// Returns { callName: [FQ callers] }
        /// </summary>
        private static Dictionary<string, List<string>> BuildCallGraph(IEnumerable<string> sourceFiles)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var file in sourceFiles)
            {
                var fileText = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(fileText);
                var root = tree.GetRoot();
                // root.ChildThatContainsPosition(3) // todo: use this to find string matches that aren't in the call graph
                var calls = root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>();

                foreach (var call in calls)
                {
                    var caller = GetFqCaller(call);
                    var callName = call.Expression.ToString();

                    if (graph.ContainsKey(callName))
                        graph[callName].Add(caller);
                    else
                        graph[callName] = new List<string> { caller };
                }
            }

            return graph;
        }

        private static string GetFqCaller(InvocationExpressionSyntax call)
        {
            var ancestors = call.Ancestors();
            var _namespace = ancestors.OfType<NamespaceDeclarationSyntax>().SingleOrDefault();
            var _classes = string.Join(".", ancestors.OfType<ClassDeclarationSyntax>().Select(c => c.Identifier));
            var _caller = GetDirectCaller(call);

            var namespaceName = _namespace == null ? "global" : _namespace.Name.ToString();

            // todo: add method params if call is ambiguous
            return $"{namespaceName}.{_classes}.{_caller}";
        }

        private static string GetDirectCaller(InvocationExpressionSyntax call)
        {
            var ancestors = call.Ancestors();
            var method = ancestors.OfType<MethodDeclarationSyntax>().SingleOrDefault();
            if (method != null) return method.Identifier.ToString();

            var field = ancestors.OfType<FieldDeclarationSyntax>().SingleOrDefault();
            if (field != null) return field.ToString();

            var property = ancestors.OfType<PropertyDeclarationSyntax>().SingleOrDefault();
            if (property != null) return property.Identifier.ToString();

            var constructor = ancestors.OfType<ConstructorDeclarationSyntax>().SingleOrDefault();
            if (constructor != null) return constructor.Identifier.ToString();

            var _operator = ancestors.OfType<OperatorDeclarationSyntax>().SingleOrDefault();
            if (_operator != null) return _operator.ToString();

            throw new InvalidOperationException($"Unable to get caller of {call.Expression}");
        }
    }

    internal static class StringExtensions
    {
        public static string[] Lines(this string text)
        {
            return text.Split(Environment.NewLine);
        }
    }
}
