using CosmosPro.ML.DemandForCast.Features.Models;
using CosmosPro.ML.DemandForCast.Forecasting.Engines;

namespace CosmosPro.ML.DemandForCast.Forecasting.Tests;

public sealed class LightGbmForecastEngineTests
{
    private static readonly DateOnly Origem = new(2025, 1, 1);

    /// <summary>
    /// Dataset sintético com sinal aprendível: a demanda é função determinística
    /// de Lag7 + um efeito de fim de semana. Um modelo decente deve capturar isso.
    /// </summary>
    private static List<FeatureVector> LearnableSet(int n)
    {
        var list = new List<FeatureVector>(n);
        var rng = new Random(7);
        for (int i = 0; i < n; i++)
        {
            var data = Origem.AddDays(i);
            var fimDeSemana = data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var lag7 = (decimal)rng.Next(5, 30);
            // alvo = lag7 * (fim de semana puxa 1.5x) + ruído pequeno
            var target = lag7 * (fimDeSemana ? 1.5m : 1.0m) + rng.Next(-2, 3);
            if (target < 0) target = 0;

            list.Add(new FeatureVector
            {
                Data = data,
                LojaId = 1,
                Sku = $"SKU{i % 5}",
                Target = target,
                IsValidTarget = true,
                Lag7 = lag7,
                Lag14 = lag7,
                Lag21 = lag7,
                Lag28 = lag7,
                RollMean7 = lag7,
                RollMean28 = lag7,
                RollMax28 = lag7,
                DiaDaSemana = (int)data.DayOfWeek,
                DiaDoMes = data.Day,
                Mes = data.Month,
                FimDeSemana = fimDeSemana,
                PrecoUnitario = 10m,
                PrecoRelativoMedia = 1m,
                Categoria = "OTC",
                PrincipioAtivo = "Dipirona",
                ClasseAbc = "A",
                UF = "SP",
                Regiao = "Sudeste",
                PerfilLoja = "Bairro",
            });
        }
        return list;
    }

    // Hiperparâmetros leves para os testes rodarem rápido.
    private static readonly LightGbmHyperparameters FastHp = new()
    {
        NumberOfIterations = 30,
        NumberOfLeaves = 16,
        MinimumExampleCountPerLeaf = 5,
    };

    [Fact]
    public void Treina_e_preve_valores_nao_negativos_plausiveis()
    {
        var data = LearnableSet(300);
        using var model = (LightGbmForecastModel)new LightGbmForecastEngine(FastHp).Fit(data);

        var pred = model.Predict(data[0]);
        pred.Should().BeGreaterThanOrEqualTo(0);
        // O alvo gira em torno de ~5..45; a previsão deve cair nessa ordem de grandeza.
        pred.Should().BeLessThan(200);
    }

    [Fact]
    public void Modelo_aprende_o_padrao_melhor_que_a_media_global()
    {
        var data = LearnableSet(400);
        using var model = (LightGbmForecastModel)new LightGbmForecastEngine(FastHp).Fit(data);

        var mediaGlobal = (double)data.Average(d => d.Target);

        double erroModelo = 0, erroMedia = 0;
        foreach (var fv in data)
        {
            erroModelo += Math.Abs(model.Predict(fv) - (double)fv.Target);
            erroMedia += Math.Abs(mediaGlobal - (double)fv.Target);
        }

        // O modelo (que vê Lag7 e fim de semana) deve errar bem menos que prever
        // sempre a média global.
        erroModelo.Should().BeLessThan(erroMedia * 0.7);
    }

    [Fact]
    public void Save_e_Load_preservam_as_previsoes()
    {
        var data = LearnableSet(200);
        using var model = (LightGbmForecastModel)new LightGbmForecastEngine(FastHp).Fit(data);

        var path = Path.Combine(Path.GetTempPath(), $"lgbm-{Guid.NewGuid():N}.zip");
        try
        {
            model.Save(path);
            File.Exists(path).Should().BeTrue();

            using var loaded = LightGbmForecastModel.Load(path);

            // Previsões do modelo carregado batem com as do original (determinístico
            // pós-treino — independe da aleatoriedade do treino).
            foreach (var fv in data.Take(20))
            {
                loaded.Predict(fv).Should().BeApproximately(model.Predict(fv), 1e-4);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Name_do_engine_eh_lightgbm()
    {
        new LightGbmForecastEngine().Name.Should().Be("lightgbm");
    }
}
