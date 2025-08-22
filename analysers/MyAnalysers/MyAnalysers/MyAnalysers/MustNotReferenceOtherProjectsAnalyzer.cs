using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyAnalysers;

/// <summary>
/// Intention: you've got a utility project in your solution that you
/// want to keep self-contained. Don't let it reference other projects.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MustNotReferenceOtherProjectsAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] AllowedReferences =
    [
        "System",
        "Microsoft",
        "mscorlib",
        "netstandard",
        "WindowsBase",
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        id: "ASDF001",
        title: "Must Not Reference Other Projects",
        messageFormat: "Assembly '{0}' references '{1}'",
        category: "ProjectReference",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var compilation = context.Compilation;
        var currentAssembly = compilation.Assembly;
        var referencedAssemblies = compilation.ReferencedAssemblyNames;

        var currentAssemblyName = currentAssembly.Name;
        if (!currentAssemblyName.StartsWith(nameof(MyAnalysers)))
            return;

        foreach (var reference in referencedAssemblies)
        {
            if (AllowedReferences.Any(x => reference.Name.StartsWith(x)))
                continue;

            if (reference.Name != currentAssemblyName)
            {
                var diagnostic = Diagnostic.Create(Rule, Location.None, currentAssemblyName, reference.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
