using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace roslynplay
{
    /// <summary>
    /// Given a solution file and a fully qualified method name, prints a tree
    /// of all method call paths that call the given method.
    /// </summary>
    internal static class CallTracer
    {
        public static async Task RunFromArgs(string[] args)
        {
            if (args.Length == 2)
            {
                var sln = args[0];
                var fqMethod = args[1];

                await TraceCallsTo(sln, fqMethod);
            }
            if (args.Length == 4)
            {
                var sln = args[0];
                var projname = args[1];
                var filename = args[2];
                var position = int.Parse(args[3]);

                await TraceCallsTo(sln, projname, filename, position);
            }
        }

        public static async Task TraceCallsTo(string slnFile, string projname, string filename, int position)
        {
            Console.WriteLine("loading msbuild");
            var workspace = LoadMsBuild();

            Console.WriteLine("loading solution");
            var solution = await workspace.OpenSolutionAsync(slnFile);

            var project = solution.Projects.Single(x => x.Name == projname);
            var document = project.Documents.Single(x => x.Name == filename);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
            Console.WriteLine($"found symbol: {symbol}. Getting all members of containing type");

            foreach (var sym in symbol.ContainingType.GetMembers())
            {
                var root = await BuildCallTree(sym, solution);
                if (root.Callers.Count > 0)
                {
                    Console.WriteLine($"member {sym}:");
                    PrintEntrypointTraces(root);
                }
            }
        }

        private static void PrintEntrypointTraces(CallTraceNode root)
        {
            var entrypoints = FindEntryPoints(root).ToList();
            foreach (var entrypoint in entrypoints)
            {
                PrintChildCallTrace(entrypoint);
            }
        }

        private static async Task<CallTraceNode> BuildCallTree(ISymbol symbol, Solution solution)
        {
            var root = new CallTraceNode(symbol);
            await TraceCalls(solution, root, exclude: ".Tests.");
            return root;
        }

        private static void PrintChildCallTrace(CallTraceNode node, int indent = 0)
        {
            var indentStr = new string(' ', indent);
            Console.WriteLine($"{indentStr}{node.Symbol}");
            if (node.Call != null)
                PrintChildCallTrace(node.Call, indent + 2);
        }

        private static IEnumerable<CallTraceNode> FindEntryPoints(CallTraceNode root)
        {
            if (root.Callers.Count == 0)
                yield return root;
            foreach (var node in root.Callers.SelectMany(FindEntryPoints))
            {
                yield return node;
            }
        }


        public static async Task TraceCallsTo(string slnFile, string fqMethodName)
        {
            var nameParts = fqMethodName.Split('.');
            var typeName = string.Join(".", nameParts[..^1]);
            var methodName = nameParts[^1];

            Console.WriteLine("loading msbuild");
            var workspace = LoadMsBuild();

            Console.WriteLine("loading solution");
            var solution = await workspace.OpenSolutionAsync(slnFile);
            var typeSymbol = await FindTypeSymbol(typeName, solution);

            Console.WriteLine("finding usages");

            var members = typeSymbol.GetMembers().Where(m => m.Name == methodName).ToList();
            if (!members.Any())
            {
                throw new InvalidOperationException($"Couldn't find member '{methodName}' in {typeName}");
            }

            foreach (var member in members)
            {
                Console.WriteLine($"Traces of {member}");
                var tree = await BuildCallTree(member, solution);
                PrintEntrypointTraces(tree);
            }
        }

        private static MSBuildWorkspace LoadMsBuild()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
            }

            var workspace = MSBuildWorkspace.Create();
            workspace.SkipUnrecognizedProjects = true;
            workspace.WorkspaceFailed += (sender, args) =>
            {
                if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    // todo: show these with verbose option
                    // Console.Error.WriteLine(args.Diagnostic.Message);
                }
            };

            return workspace;
        }

        private static async Task<INamedTypeSymbol> FindTypeSymbol(string typeName, Solution solution)
        {
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();

                if (compilation == null)
                    throw new InvalidOperationException($"couldn't load compilation for project {project.Name}");

                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol == null) continue;

                Console.WriteLine($"found type in {project.Name}");
                return typeSymbol;
            }

            throw new InvalidOperationException($"Couldn't find type {typeName}");
        }

        private static async Task TraceCalls(
            Solution solution,
            CallTraceNode traceNode,
            int depth = 0,
            string? exclude = null)
        {
            if (depth > 20) throw new InvalidOperationException("too deep!");

            var callers = await SymbolFinder.FindCallersAsync(traceNode.Symbol, solution);

            foreach (var caller in callers)
            {
                if (ExcludeSymbol(caller.CallingSymbol, exclude))
                    continue;
                var callingNode = new CallTraceNode(caller.CallingSymbol, traceNode);
                traceNode.Callers.Add(callingNode);
                await TraceCalls(solution, callingNode, depth + 1, exclude);
            }
        }

        private static bool ExcludeSymbol(ISymbol symbol, string? exclude)
        {
            if (exclude == null) return false;

            var callerStr = symbol.ToString();
            
            if (callerStr != null && callerStr.Contains(exclude))
            {
                return true;
            }

            var callingNamespace = symbol.ContainingNamespace.ToString();
            if (callingNamespace != null && callingNamespace.Contains(exclude))
            {
                return true;
            }

            return false;
        }

        private class CallTraceNode(ISymbol symbol, CallTraceNode? call = null)
        {
            public ISymbol Symbol { get; init; } = symbol;
            public CallTraceNode? Call { get; } = call;
            public List<CallTraceNode> Callers { get; set; } = [];
        }
    }
}
