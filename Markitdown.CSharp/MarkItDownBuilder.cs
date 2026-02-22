using MarkItDown.CSharp.Interfaces;

namespace MarkItDown.CSharp;

/// <summary>
/// Fluent builder to create a <see cref="MarkItDown"/> instance with custom converters.
/// Add converters via <see cref="AddConverter"/>, then call <see cref="Build"/> to obtain the engine.
/// </summary>
public sealed class MarkItDownBuilder
{
    private readonly List<(IDocumentConverter Converter, double Priority)> _converters = new();

    /// <summary>
    /// Adds a converter that will be registered when <see cref="Build"/> is called.
    /// Lower priority values are tried first. If the converter declares
    /// <see cref="IDocumentConverter.SupportedExtensions"/>, the engine will try it first for those extensions.
    /// </summary>
    public MarkItDownBuilder AddConverter(IDocumentConverter converter, double priority = 0.0)
    {
        _converters.Add((converter, priority));
        return this;
    }

    /// <summary>
    /// Creates a <see cref="MarkItDown"/> instance: disables built-ins in options, optionally
    /// enables them on the new instance, then registers all converters added via <see cref="AddConverter"/>.
    /// </summary>
    /// <param name="options">Options for the engine. If null, default options are used. Built-in converters are disabled in the options passed to the constructor.</param>
    /// <param name="includeBuiltins">If true, the new instance will have built-in converters (DOCX, PDF, etc.) enabled before custom converters are registered.</param>
    /// <returns>A configured <see cref="MarkItDown"/> instance.</returns>
    public MarkItDown Build(MarkItDownOptions? options = null, bool includeBuiltins = true)
    {
        options ??= new MarkItDownOptions();
        var optionsWithoutBuiltins = new MarkItDownOptions
        {
            HttpClient = options.HttpClient,
            EnableBuiltins = false,
            EnablePlugins = options.EnablePlugins,
            Plugins = options.Plugins,
            LlmClient = options.LlmClient,
            LlmModel = options.LlmModel,
            LlmPrompt = options.LlmPrompt,
            ExifToolPath = options.ExifToolPath,
            StyleMap = options.StyleMap,
            AudioTranscriptionService = options.AudioTranscriptionService,
        };

        var markItDown = new MarkItDown(optionsWithoutBuiltins);

        if (includeBuiltins)
        {
            markItDown.EnableBuiltins();
        }

        foreach (var (converter, priority) in _converters)
        {
            markItDown.RegisterConverter(converter, priority);
        }

        return markItDown;
    }
}
