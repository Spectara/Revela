using System.Globalization;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Spectara.Revela.Core.Themes;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Scriban function <c>t "key" arg0 arg1 …</c> returning the theme UI string for the site language.
/// </summary>
/// <remarks>
/// Implemented as <see cref="IScriptCustomFunction"/> (instead of an imported delegate) so it can
/// accept a variable number of placeholder arguments without reflection, keeping it trim-safe.
/// The result is plain text: templates must escape it (<c>html_escape (t "key")</c>).
/// </remarks>
internal sealed class TranslateFunction(ThemeStrings strings) : IScriptCustomFunction
{
    public int RequiredParameterCount => 1;

    public int ParameterCount => 1;

    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.Direct;

    public Type ReturnType => typeof(string);

    public ScriptParameterInfo GetParameterInfo(int index) =>
        index == 0
            ? new ScriptParameterInfo(typeof(string), "key")
            : new ScriptParameterInfo(typeof(object), "args");

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        if (arguments.Count == 0 || arguments[0] is null)
        {
            throw new ScriptRuntimeException(callerContext?.Span ?? default, "Function 't' expects a translation key, e.g. t \"photo.close\".");
        }

        var key = Convert.ToString(arguments[0], CultureInfo.InvariantCulture) ?? string.Empty;
        var args = arguments.Count > 1 ? arguments.Skip(1).ToArray() : [];
        return strings.Translate(key, args);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement) =>
        new(Invoke(context, callerContext, arguments, blockStatement));
}
