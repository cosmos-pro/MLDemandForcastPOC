using CosmosPro.ML.DemandForCast.Features.Models;

namespace CosmosPro.ML.DemandForCast.Forecasting.Evaluation;

public sealed record WalkForwardOptions
{
    /// <summary>Número de janelas de teste consecutivas avaliadas (do passado recente ao fim).</summary>
    public int NumberOfFolds { get; init; } = 4;

    /// <summary>Tamanho de cada janela de teste, em dias.</summary>
    public int TestWindowDays { get; init; } = 14;

    /// <summary>Histórico mínimo (dias) exigido antes do primeiro fold para treinar.</summary>
    public int MinTrainDays { get; init; } = 35;
}

public sealed record FoldResult(
    int Fold,
    DateOnly TrainAte,
    DateOnly TesteInicio,
    DateOnly TesteFim,
    int TrainSize,
    int TestSize,
    ForecastMetrics Metrics);

public sealed record BacktestResult(
    string Engine,
    ForecastMetrics Global,
    IReadOnlyList<FoldResult> Folds,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ForecastMetrics>> PorDimensao);

/// <summary>
/// Avaliação walk-forward (origem rolante, janela de treino expansível): para cada
/// fold, treina com tudo ANTES da janela de teste e mede o erro DENTRO da janela.
/// Nunca embaralha o tempo (CLAUDE.md §6). Linhas em ruptura (IsValidTarget=false)
/// são excluídas de treino e avaliação — venda observada não reflete demanda.
/// </summary>
public sealed class WalkForwardBacktest(WalkForwardOptions? options = null)
{
    private readonly WalkForwardOptions _opt = options ?? new WalkForwardOptions();

    public BacktestResult Run(IForecastEngine engine, IReadOnlyList<FeatureVector> features)
    {
        var valid = features.Where(f => f.IsValidTarget).OrderBy(f => f.Data).ToList();
        if (valid.Count == 0)
            return new BacktestResult(engine.Name, ForecastMetrics.Compute([]), [], EmptyDims());

        var minDate = valid[0].Data;
        var maxDate = valid[^1].Data;

        // Os folds são as últimas N janelas de TestWindowDays terminando em maxDate.
        // O primeiro fold só roda se sobrar MinTrainDays de histórico antes dele.
        var folds = new List<FoldResult>();
        var allPairs = new List<Prediction>();

        for (int i = _opt.NumberOfFolds - 1; i >= 0; i--)
        {
            var testeFim = maxDate.AddDays(-i * _opt.TestWindowDays);
            var testeInicio = testeFim.AddDays(-_opt.TestWindowDays + 1);
            var trainAte = testeInicio.AddDays(-1);

            if (trainAte.DayNumber - minDate.DayNumber + 1 < _opt.MinTrainDays)
                continue; // histórico insuficiente para este fold

            var train = valid.Where(f => f.Data <= trainAte).ToList();
            var test = valid.Where(f => f.Data >= testeInicio && f.Data <= testeFim).ToList();
            if (train.Count == 0 || test.Count == 0) continue;

            var model = engine.Fit(train);

            var foldPairs = new List<Prediction>(test.Count);
            foreach (var fv in test)
            {
                var pred = Math.Max(0, model.Predict(fv)); // demanda não-negativa
                foldPairs.Add(new Prediction(fv, (double)fv.Target, pred));
            }
            allPairs.AddRange(foldPairs);

            // Modelos com recursos nativos (ex.: PredictionEngine do LightGBM) são
            // recriados a cada fold — libera para não vazar entre folds.
            if (model is IDisposable disposable) disposable.Dispose();

            var foldMetrics = ForecastMetrics.Compute(foldPairs.Select(p => (p.Actual, p.Predicted)).ToList());
            folds.Add(new FoldResult(
                folds.Count + 1, trainAte, testeInicio, testeFim, train.Count, test.Count, foldMetrics));
        }

        var global = ForecastMetrics.Compute(allPairs.Select(p => (p.Actual, p.Predicted)).ToList());
        var porDim = AggregateByDimension(allPairs);
        return new BacktestResult(engine.Name, global, folds, porDim);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ForecastMetrics>> AggregateByDimension(
        List<Prediction> pairs)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, ForecastMetrics>>();

        result["Categoria"] = GroupMetrics(pairs, p => p.Features.Categoria);
        result["ClasseAbc"] = GroupMetrics(pairs, p => p.Features.ClasseAbc);
        result["Loja"] = GroupMetrics(pairs, p => p.Features.LojaId.ToString());
        result["UF"] = GroupMetrics(pairs, p => p.Features.UF);

        return result;
    }

    private static IReadOnlyDictionary<string, ForecastMetrics> GroupMetrics(
        List<Prediction> pairs, Func<Prediction, string> keySelector) =>
        pairs.GroupBy(keySelector)
             .Where(g => !string.IsNullOrEmpty(g.Key))
             .ToDictionary(
                 g => g.Key,
                 g => ForecastMetrics.Compute(g.Select(p => (p.Actual, p.Predicted)).ToList()));

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ForecastMetrics>> EmptyDims() =>
        new Dictionary<string, IReadOnlyDictionary<string, ForecastMetrics>>();

    private readonly record struct Prediction(FeatureVector Features, double Actual, double Predicted);
}
