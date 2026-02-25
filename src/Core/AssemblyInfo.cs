using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spectara.Revela.Core.Tests")]

// Template/Asset keys use lowercase by convention for web URLs.
// ToLowerInvariant() is format conversion, not normalization.
[assembly: SuppressMessage(
    "Globalization",
    "CA1308:Normalize strings to uppercase",
    Scope = "namespaceanddescendants",
    Target = "~N:Spectara.Revela.Core.Services",
    Justification = "Template/asset keys use lowercase by convention for web URLs")]
