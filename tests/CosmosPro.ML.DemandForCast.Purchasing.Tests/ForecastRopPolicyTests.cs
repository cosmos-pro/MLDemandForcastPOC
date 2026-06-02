using CosmosPro.ML.DemandForCast.Purchasing;
using CosmosPro.ML.DemandForCast.Purchasing.Policies;

namespace CosmosPro.ML.DemandForCast.Purchasing.Tests;

public sealed class ForecastRopPolicyTests
{
    private static PolicyContext Ctx(
        IReadOnlyList<decimal>? historico = null,
        IReadOnlyList<decimal>? forecast = null,
        IReadOnlyList<decimal>? residuos = null,
        int lt = 7,
        int ciclo = 7,
        double z = 1.65) => new()
        {
            Sku = "SKU1",
            LojaId = 1,
            Hoje = new DateOnly(2026, 1, 1),
            LeadTimeDias = lt,
            CicloDias = ciclo,
            FatorServico = z,
            EstoqueAtual = 0,
            EmTransito = 0,
            HistoricoVendas = historico ?? [],
            ForecastFuturo = forecast,
            ResiduosForecast = residuos,
        };

    [Fact]
    public void s_eh_soma_do_forecast_no_LT_mais_safety_do_residuo()
    {
        // Forecast: [10, 20, 30, 40, 50, 60, 70 | 80, 90, 100, 110, 120, 130, 140]
        // LT=7, ciclo=7. Σ(LT) = 280; Σ(LT+ciclo) = 1050.
        var fc = Enumerable.Range(1, 14).Select(i => (decimal)(i * 10)).ToArray();
        // Resíduos com σ exato (gerados para ter desvio amostral conhecido).
        var residuos = new decimal[] { -5, -5, 5, 5, -5, -5, 5, 5 }; // σ ≈ 5.345 (n=8)
        var policy = new ForecastRopPolicy();

        var p = policy.Compute(Ctx(forecast: fc, residuos: residuos, lt: 7, ciclo: 7, z: 1.0));

        var sigma = StdDev(residuos);
        var safetyEsperado = (decimal)(1.0 * sigma * Math.Sqrt(7));
        p.ReorderPoint.Should().BeApproximately(280m + safetyEsperado, 0.01m);
        p.OrderUpToLevel.Should().BeApproximately(1050m + safetyEsperado, 0.01m);
    }

    [Fact]
    public void Sem_forecast_cai_para_media_historica()
    {
        // Histórico μ=10. LT=5, ciclo=3. Esperado: s ≈ 50, S ≈ 80.
        var hist = Enumerable.Repeat(10m, 28).ToList();
        var p = new ForecastRopPolicy().Compute(Ctx(historico: hist, lt: 5, ciclo: 3, z: 0));

        p.ReorderPoint.Should().BeApproximately(50m, 0.001m);
        p.OrderUpToLevel.Should().BeApproximately(80m, 0.001m);
    }

    [Fact]
    public void Forecast_negativo_eh_clampado_em_zero()
    {
        // Modelo nunca deveria prever negativo, mas se aparecer não pode estourar o ROP pra baixo.
        var fc = new decimal[] { -10, -5, 0, 5, 10, 15, 20 };
        var p = new ForecastRopPolicy().Compute(Ctx(forecast: fc, lt: 7, ciclo: 0, z: 0));

        // Σ esperada: 0 + 0 + 0 + 5 + 10 + 15 + 20 = 50.
        p.ReorderPoint.Should().Be(50m);
    }

    [Fact]
    public void Quando_sigma_de_residuo_eh_menor_que_sigma_da_demanda_safety_eh_menor()
    {
        // Esta é a justificativa metodológica da política: σ do erro do forecast
        // deveria ser menor que σ da demanda crua (porque o modelo captura
        // sazonalidade/promoção/etc.) — então safety encolhe.
        var demanda = new decimal[] { 5, 25, 5, 25, 5, 25, 5, 25, 5, 25 };  // σ alto
        var residuosForecast = new decimal[] { 1, -1, 1, -1, 1, -1, 1, -1, 1, -1 }; // σ baixo
        var fc = Enumerable.Repeat(15m, 14).ToArray();

        var pSemResiduo = new ForecastRopPolicy().Compute(
            Ctx(historico: demanda, forecast: fc, residuos: null, z: 1.65));
        var pComResiduo = new ForecastRopPolicy().Compute(
            Ctx(historico: demanda, forecast: fc, residuos: residuosForecast, z: 1.65));

        // ReorderPoint = Σ forecast no LT (mesma) + safety. Só safety muda.
        pComResiduo.ReorderPoint.Should().BeLessThan(pSemResiduo.ReorderPoint);
    }

    private static double StdDev(decimal[] xs)
    {
        if (xs.Length < 2) return 0;
        var arr = xs.Select(x => (double)x).ToArray();
        var mean = arr.Average();
        var sumSq = arr.Sum(x => (x - mean) * (x - mean));
        return Math.Sqrt(sumSq / (arr.Length - 1));
    }
}
