using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Models;

// Opt SDK types that are surfaced to Scriban templates into the trim-safe
// [RevelaTemplateModel] source generator. These live in the SDK (which has no
// Scriban dependency by design), so the ToScriptObject() extension method is
// generated here in Features/Generate where Scriban is already referenced.
//
// See Spectara.Revela.Sdk.Abstractions.RevelaTemplateModelAttribute for the
// rationale and Spectara.Revela.Sdk.Generators.TemplateModelGenerator for the
// generator that consumes these markings.
[assembly: RevelaTemplateModel(typeof(ExifData))]
[assembly: RevelaTemplateModel(typeof(IBuildInfo))]
