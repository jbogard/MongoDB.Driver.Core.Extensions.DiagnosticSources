using System.Reflection;

namespace MongoDB.Driver.Core.Extensions.DiagnosticSources;

internal static class SignalVersionHelper
{
    public static string GetVersion<T>()
    {
        return typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    }
}