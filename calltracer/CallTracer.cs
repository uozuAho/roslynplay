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
            Console.WriteLine($"foud symbol: {symbol}. Calls:");

            await TraceCalls(solution, symbol);
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
                await TraceCalls(solution, member, exclude: ".Tests.");
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

        private static async Task TraceCalls(Solution solution, ISymbol symbol, int depth=0, string? exclude = null)
        {
            if (depth > 20)
            {
                throw new InvalidOperationException("too deep!");
            }

            var callers = await SymbolFinder.FindCallersAsync(symbol, solution);
            var indent = new string(' ', depth * 2);

            foreach (var caller in callers)
            {
                if (ExcludeSymbol(caller.CallingSymbol, exclude)) continue;
                Console.WriteLine($"{indent}{caller.CallingSymbol}");
                await TraceCalls(solution, caller.CallingSymbol, depth + 1, exclude);
            }
        }

        private static async Task TraceCalls2(
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
                await TraceCalls2(solution, callingNode, depth + 1, exclude);
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

        class CallTraceNode
        {
            public ISymbol Symbol { get; init; }
            public CallTraceNode Calls { get; }
            public List<CallTraceNode> Callers { get; set; } = new();

            private CallTraceNode()
            {
                throw new InvalidOperationException("don't call this");
            }

            public CallTraceNode(ISymbol symbol, CallTraceNode calls)
            {
                Symbol = symbol;
                Calls = calls;
            }
        }
    }
}
