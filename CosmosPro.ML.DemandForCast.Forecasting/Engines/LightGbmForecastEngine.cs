using CosmosPro.ML.DemandForCast.Features.Models;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;

namespace CosmosPro.ML.DemandForCast.Forecasting.Engines;

/// <summary>
/// Engine principal (CLAUDE.md §1): **modelo global** LightGBM sobre as features de
/// F5, com SKU e demais categóricas via one-hot. Um único modelo cobre todos os
/// SKUs/lojas (SKU entra como feature) — treinar um modelo por SKU é inviável em
/// escala farma.
/// </summary>
public sealed class LightGbmForecastEngine : IForecastEngine
{
    private readonly LightGbmHyperparameters _hp;

    public LightGbmForecastEngine(LightGbmHyperparameters? hyperparameters = null)
    {
        _hp = hyperparameters ?? new LightGbmHyperparameters();
    }

    public string Name => "lightgbm";

    public IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet)
    {
        // Seed no MLContext para reprodutibilidade do treino.
        var mlContext = new MLContext(seed: _hp.Seed);

        var rows = trainSet.Select(LightGbmInput.From).ToList();
        var data = mlContext.Data.LoadFromEnumerable(rows);

        // One-hot das categóricas → "<col>_oh", depois concatena tudo em "Features".
        var oneHotPairs = LightGbmInput.CategoricalColumns
            .Select(c => new InputOutputColumnPair($"{c}_oh", c))
            .ToArray();

        var featureColumns = LightGbmInput.NumericColumns
            .Concat(LightGbmInput.CategoricalColumns.Select(c => $"{c}_oh"))
            .ToArray();

        var options = new LightGbmRegressionTrainer.Options
        {
            LabelColumnName = nameof(LightGbmInput.Label),
            FeatureColumnName = "Features",
            NumberOfLeaves = _hp.NumberOfLeaves,
            NumberOfIterations = _hp.NumberOfIterations,
            LearningRate = _hp.LearningRate,
            MinimumExampleCountPerLeaf = _hp.MinimumExampleCountPerLeaf,
        };

        var pipeline = mlContext.Transforms.Categorical
            .OneHotEncoding(oneHotPairs)
            .Append(mlContext.Transforms.Concatenate("Features", featureColumns))
            .AppendCacheCheckpoint(mlContext)
            .Append(mlContext.Regression.Trainers.LightGbm(options));

        var model = pipeline.Fit(data);
        return new LightGbmForecastModel(mlContext, model, data.Schema);
    }
}

/// <summary>Hiperparâmetros do LightGBM. Defaults conservadores para o POC.</summary>
public sealed record LightGbmHyperparameters
{
    public int NumberOfLeaves { get; init; } = 31;
    public int NumberOfIterations { get; init; } = 100;
    public double LearningRate { get; init; } = 0.1;
    public int MinimumExampleCountPerLeaf { get; init; } = 20;
    public int Seed { get; init; } = 42;
}
