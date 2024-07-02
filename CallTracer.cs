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
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: dotnet run <sln file path> <FQ method>");
            }
            var sln = args[0];
            var fqMethod = args[1];

            await TraceCallsTo(sln, fqMethod);
        }

        public static async Task TraceCallsTo(string slnFile, string fqMethodName)
        {
            var nameParts = fqMethodName.Split('.');
            var typeName = string.Join(".", nameParts[..^1]);
            var methodName = nameParts[^1];

            Console.WriteLine("loading msbuild");
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
                    Console.Error.WriteLine(args.Diagnostic.Message);
                }
            };

            Console.WriteLine("loading solution");
            var solution = await workspace.OpenSolutionAsync(slnFile);

            var project = solution.Projects.First();
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                throw new InvalidOperationException($"couldn't load compilation for project {project.Name}");
            }
            Console.WriteLine($"using project: {project.Name}");

            var typeSymbol = compilation.GetTypeByMetadataName(typeName);
            if (typeSymbol == null)
            {
                throw new InvalidOperationException($"Couldn't find type {typeName}");
            }

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
                if (exclude != null)
                {
                    var callerStr = caller.CallingSymbol.ToString();
                    if (callerStr != null && callerStr.Contains(exclude))
                    {
                        continue;
                    }
                    if (caller.CallingSymbol.ContainingNamespace.ToString().Contains(exclude))
                    {
                        continue;
                    }
                }
                Console.WriteLine($"{indent}{caller.CallingSymbol}");
                await TraceCalls(solution, caller.CallingSymbol, depth + 1, exclude);
            }
        }
    }
}
