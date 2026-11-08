using System.Reflection;

namespace CohEngine.Core;

/// <summary>Build identity of the engine library.</summary>
public static class EngineInfo
{
    /// <summary>Gets the version MinVer stamped into the assembly (the informational version).</summary>
    public static string Version { get; } =
        typeof(EngineInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
