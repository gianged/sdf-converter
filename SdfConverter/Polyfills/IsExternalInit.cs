// Polyfill to enable C# 9+ record types in .NET Framework 4.8
// See: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/init

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
