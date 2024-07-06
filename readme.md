# C# call tracer

Little project to learn about roslyn & related tools.

Goal: find all paths to a given method call, from all program entry points.

Usage: dotnet run <sln file> <fully qualified method name (namespace.type.method)>

For example:

```sh
cd calltracer
dotnet run ../roslynplay.sln roslynplay.CallTracer.TraceCallsTo

# works with external types too:
dotnet run ../roslynplay.sln Microsoft.Build.Locator.MSBuildLocator.IsRegistered
```

todo
- detect recursive methods, prevent infinite recursion
- later: symbolfinder:
    - reverse trace
    - eliminate dupes
    - show project(s) of entrypoints