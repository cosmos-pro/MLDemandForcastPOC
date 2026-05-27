using CosmosPro.ML.DemandForCast.Forecasting.Evaluation;

namespace CosmosPro.ML.DemandForCast.Forecasting.Tests;

public sealed class ForecastMetricsTests
{
    [Fact]
    public void Previsao_perfeita_zera_todos_os_erros()
    {
        var pairs = new (double, double)[] { (10, 10), (5, 5), (0, 0) };
        var m = ForecastMetrics.Compute(pairs);

        m.Mae.Should().Be(0);
        m.Rmse.Should().Be(0);
        m.Wape.Should().Be(0);
        m.Mape.Should().Be(0);
    }

    [Fact]
    public void MAE_e_RMSE_calculados_corretamente()
    {
        // erros: +2, -4 → |2|,|4| → MAE = 3; RMSE = sqrt((4+16)/2) = sqrt(10).
        var pairs = new (double, double)[] { (10, 12), (10, 6) };
        var m = ForecastMetrics.Compute(pairs);

        m.Mae.Should().BeApproximately(3.0, 1e-9);
        m.Rmse.Should().BeApproximately(Math.Sqrt(10), 1e-9);
    }

    [Fact]
    public void WAPE_eh_soma_erros_absolutos_sobre_soma_dos_reais()
    {
        // |12-10| + |6-10| = 6 ; soma reais = 20 → WAPE = 0.3.
        var pairs = new (double, double)[] { (10, 12), (10, 6) };
        var m = ForecastMetrics.Compute(pairs);
        m.Wape.Should().BeApproximately(0.3, 1e-9);
    }

    [Fact]
    public void MAPE_ignora_pontos_com_real_zero()
    {
        // Ponto (0, 5) não entra no MAPE (divisão por zero). Só (10,12): 0.2.
        var pairs = new (double, double)[] { (10, 12), (0, 5) };
        var m = ForecastMetrics.Compute(pairs);
        m.Mape.Should().BeApproximately(0.2, 1e-9);

        // Mas WAPE considera o erro do ponto zero: (2 + 5) / 10 = 0.7.
        m.Wape.Should().BeApproximately(0.7, 1e-9);
    }

    [Fact]
    public void Todos_reais_zero_resulta_em_MAPE_nulo_e_WAPE_zero()
    {
        var pairs = new (double, double)[] { (0, 0), (0, 0) };
        var m = ForecastMetrics.Compute(pairs);
        m.Mape.Should().BeNull();
        m.Wape.Should().Be(0);
    }

    [Fact]
    public void Conjunto_vazio_nao_quebra()
    {
        var m = ForecastMetrics.Compute([]);
        m.N.Should().Be(0);
        m.Mape.Should().BeNull();
    }
}
