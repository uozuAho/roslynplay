using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace roslynplay
{
    internal class Finder
    {
        internal static async Task RunFromArgs(string[] args)
        {
            var project = args[0];
            var sourceFile = args[1];
            var position = int.Parse(args[2]);

            var symbol = await FindSymbolAt(project, sourceFile, position);

            Console.WriteLine(symbol);
        }

        public static async Task<ISymbol> FindSymbolAt(string projectPath, string sourcePath, int position)
        {
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

            var project = await workspace.OpenProjectAsync(projectPath);
            var doc = project.Documents.Single(x => x.FilePath.Contains(sourcePath));
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(doc, position);

            if (symbol == null) throw new InvalidOperationException("Couldn't find it");

            return symbol;
        }
    }
}
