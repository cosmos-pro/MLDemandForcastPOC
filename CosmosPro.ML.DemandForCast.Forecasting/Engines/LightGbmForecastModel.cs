using CosmosPro.ML.DemandForCast.Features.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CosmosPro.ML.DemandForCast.Forecasting.Engines;

/// <summary>
/// Modelo LightGBM treinado. Encapsula o <see cref="ITransformer"/> e um
/// <see cref="PredictionEngine{TSrc,TDst}"/> para inferência linha-a-linha (não é
/// thread-safe — uso sequencial no backtest/sugestão). Serializa para .zip via
/// <see cref="Save"/> / <see cref="Load"/>.
/// </summary>
public sealed class LightGbmForecastModel : IForecastModel, IDisposable
{
    private readonly MLContext _mlContext;
    private readonly ITransformer _transformer;
    private readonly DataViewSchema _inputSchema;
    private readonly PredictionEngine<LightGbmInput, LightGbmOutput> _engine;

    internal LightGbmForecastModel(MLContext mlContext, ITransformer transformer, DataViewSchema inputSchema)
    {
        _mlContext = mlContext;
        _transformer = transformer;
        _inputSchema = inputSchema;
        _engine = mlContext.Model.CreatePredictionEngine<LightGbmInput, LightGbmOutput>(transformer);
    }

    public double Predict(FeatureVector features) =>
        _engine.Predict(LightGbmInput.From(features)).Score;

    /// <summary>Persiste o modelo (pipeline + schema) no stream, formato .zip do ML.NET.</summary>
    public void Save(Stream stream) =>
        _mlContext.Model.Save(_transformer, _inputSchema, stream);

    public void Save(string path)
    {
        using var fs = File.Create(path);
        Save(fs);
    }

    public static LightGbmForecastModel Load(Stream stream)
    {
        var mlContext = new MLContext();
        var transformer = mlContext.Model.Load(stream, out var schema);
        return new LightGbmForecastModel(mlContext, transformer, schema);
    }

    public static LightGbmForecastModel Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }

    public void Dispose() => _engine.Dispose();
}
