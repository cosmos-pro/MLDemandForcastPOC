using CosmosPro.ML.DemandForCast.Features.Models;
using CosmosPro.ML.DemandForCast.Forecasting;
using CosmosPro.ML.DemandForCast.Forecasting.Engines;
using CosmosPro.ML.DemandForCast.Forecasting.Evaluation;

namespace CosmosPro.ML.DemandForCast.Forecasting.Tests;

public sealed class WalkForwardBacktestTests
{
    private static readonly DateOnly Origem = new(2025, 1, 1);

    private static FeatureVector Fv(int dayOffset, decimal target, bool valid = true,
        decimal lag7 = 0, decimal rollMean28 = 0, string categoria = "OTC", int loja = 1) => new()
    {
        Data = Origem.AddDays(dayOffset),
        LojaId = loja,
        Sku = "SKU1",
        Target = target,
        IsValidTarget = valid,
        Lag7 = lag7,
        RollMean28 = rollMean28,
        Categoria = categoria,
        ClasseAbc = "A",
        UF = "SP",
    };

    [Fact]
    public void Naive_sazonal_preve_o_Lag7()
    {
        var model = new NaiveSeasonalEngine().Fit([]);
        model.Predict(Fv(0, 0, lag7: 42)).Should().Be(42);
    }

    [Fact]
    public void Media_movel_preve_o_RollMean28()
    {
        var model = new MovingAverageEngine().Fit([]);
        model.Predict(Fv(0, 0, rollMean28: 17.5m)).Should().Be(17.5);
    }

    [Fact]
    public void Treino_de_cada_fold_nunca_ve_datas_dentro_ou_apos_a_janela_de_teste()
    {
        // 120 dias de histórico. Engine espião registra a maior data vista no Fit
        // e o início de cada teste; nenhuma data de treino pode alcançar o teste.
        var features = Enumerable.Range(0, 120)
            .Select(i => Fv(i, target: 10, lag7: 10, rollMean28: 10))
            .ToList();

        var spy = new SpyEngine();
        var opt = new WalkForwardOptions { NumberOfFolds = 4, TestWindowDays = 14, MinTrainDays = 35 };
        new WalkForwardBacktest(opt).Run(spy, features);

        spy.Fits.Should().NotBeEmpty();
        foreach (var fit in spy.Fits)
        {
            // Toda data de treino é estritamente anterior à primeira data prevista no fold.
            fit.MaxTrainDate.Should().BeBefore(fit.FirstTestDateAfterFit);
        }
    }

    [Fact]
    public void Linhas_em_ruptura_sao_excluidas_da_avaliacao()
    {
        // Série onde metade dos dias está em ruptura. O backtest só deve pontuar
        // os válidos. Usamos naive: previsão = lag7.
        var features = new List<FeatureVector>();
        for (int i = 0; i < 80; i++)
        {
            bool ruptura = i % 2 == 0;
            features.Add(Fv(i, target: 10, valid: !ruptura, lag7: 10, rollMean28: 10));
        }

        var result = new WalkForwardBacktest(new WalkForwardOptions { NumberOfFolds = 2, TestWindowDays = 10, MinTrainDays = 20 })
            .Run(new NaiveSeasonalEngine(), features);

        // Todos os pares avaliados têm target 10 e lag7 10 → erro zero, WAPE 0.
        result.Global.N.Should().BeGreaterThan(0);
        result.Global.Wape.Should().Be(0);
    }

    [Fact]
    public void Previsao_negativa_eh_clampada_em_zero()
    {
        var features = Enumerable.Range(0, 60).Select(i => Fv(i, target: 5, lag7: 5, rollMean28: 5)).ToList();
        var result = new WalkForwardBacktest(new WalkForwardOptions { NumberOfFolds = 1, TestWindowDays = 10, MinTrainDays = 35 })
            .Run(new NegativeEngine(), features);

        // Engine devolve -100; clamp em 0. Erro absoluto = |0 - 5| = 5 por ponto → WAPE 1.0.
        result.Global.Wape.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Folds_sem_historico_minimo_sao_pulados()
    {
        // Só 40 dias, MinTrainDays 35, TestWindow 14, 4 folds → a maioria dos folds
        // não tem histórico suficiente; deve rodar menos folds que o pedido.
        var features = Enumerable.Range(0, 40).Select(i => Fv(i, 10, lag7: 10)).ToList();
        var result = new WalkForwardBacktest(new WalkForwardOptions { NumberOfFolds = 4, TestWindowDays = 14, MinTrainDays = 35 })
            .Run(new NaiveSeasonalEngine(), features);

        result.Folds.Count.Should().BeLessThan(4);
    }

    [Fact]
    public void Metricas_por_dimensao_sao_segmentadas()
    {
        var features = new List<FeatureVector>();
        for (int i = 0; i < 80; i++)
        {
            var cat = i % 2 == 0 ? "OTC" : "Controlado";
            features.Add(Fv(i, target: 10, lag7: 10, rollMean28: 10, categoria: cat));
        }

        var result = new WalkForwardBacktest(new WalkForwardOptions { NumberOfFolds = 2, TestWindowDays = 10, MinTrainDays = 20 })
            .Run(new NaiveSeasonalEngine(), features);

        result.PorDimensao.Should().ContainKey("Categoria");
        result.PorDimensao["Categoria"].Keys.Should().Contain(["OTC", "Controlado"]);
    }

    // --- Engines auxiliares ---------------------------------------------------

    private sealed class SpyEngine : IForecastEngine
    {
        public List<(DateOnly MaxTrainDate, DateOnly FirstTestDateAfterFit)> Fits { get; } = [];
        public string Name => "spy";

        // O backtest chama Fit(train) e depois Predict no teste. Capturamos a maior
        // data de treino; a primeira data de teste é registrada no primeiro Predict.
        public IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet)
        {
            var maxTrain = trainSet.Max(f => f.Data);
            var record = (maxTrain, DateOnly.MaxValue);
            Fits.Add(record);
            return new SpyModel(this, Fits.Count - 1);
        }

        private sealed class SpyModel(SpyEngine owner, int fitIndex) : IForecastModel
        {
            private bool _firstSeen;
            public double Predict(FeatureVector features)
            {
                if (!_firstSeen)
                {
                    var (maxTrain, _) = owner.Fits[fitIndex];
                    owner.Fits[fitIndex] = (maxTrain, features.Data);
                    _firstSeen = true;
                }
                return (double)features.Lag7;
            }
        }
    }

    private sealed class NegativeEngine : IForecastEngine
    {
        public string Name => "negative";
        public IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet) => new M();
        private sealed class M : IForecastModel
        {
            public double Predict(FeatureVector features) => -100;
        }
    }
}
