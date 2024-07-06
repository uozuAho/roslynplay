# C# call tracer

Little project to learn about roslyn & related tools.

Goal: find all paths to a given method call, from all program entry points.

Example usages

```sh
cd calltracer
## TRACE CALLS
# by fully qualified method name
dotnet run trace ../roslynplay.sln roslynplay.CallTracer.TraceCallsTo
# by sln, project, file, character position
dotnet run trace ../roslynplay.sln roslynplay CallTracer.cs 871

# works with external types too:
dotnet run trace ../roslynplay.sln Microsoft.Build.Locator.MSBuildLocator.IsRegistered
dotnet run trace ../roslynplay.sln MoreLinq.MoreEnumerable.Choose

# extension types are found in their namespaces:
dotnet run trace ../roslynplay.sln mylib.MyStringExtensions.AddChar  # works
dotnet run trace ../roslynplay.sln System.String.AddChar             # String.AddChar not found

## FIND SYMBOLS
dotnet run find roslynplay.csproj Program.cs 198  # 198 = character position
> roslynplay.CallTracer.RunFromArgs(string[])

dotnet run find roslynplay.csproj Finder.cs 508
> System.Console.WriteLine(object?)
```

todo
- reverse trace
- add exclude args to tracer (exclude kai.integration)
- grep first lines only (entry points)
- find other call/overrides
    - any way to find all overrides?
- detect recursive methods, prevent infinite recursion