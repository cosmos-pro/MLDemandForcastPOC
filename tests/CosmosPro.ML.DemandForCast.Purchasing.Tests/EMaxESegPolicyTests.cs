using CosmosPro.ML.DemandForCast.Purchasing;
using CosmosPro.ML.DemandForCast.Purchasing.Policies;

namespace CosmosPro.ML.DemandForCast.Purchasing.Tests;

public sealed class EMaxESegPolicyTests
{
    private static PolicyContext Ctx(IReadOnlyList<decimal> historico, int lt = 7, int ciclo = 7, double z = 1.65) =>
        new()
        {
            Sku = "SKU1",
            LojaId = 1,
            Hoje = new DateOnly(2026, 1, 1),
            LeadTimeDias = lt,
            CicloDias = ciclo,
            FatorServico = z,
            EstoqueAtual = 0,
            EmTransito = 0,
            HistoricoVendas = historico,
        };

    [Fact]
    public void Quando_historico_constante_eSeg_eh_zero_e_S_eh_mu_vezes_LT_mais_ciclo()
    {
        // Demanda constante = 10 unidades/dia → σ = 0 → safety = 0.
        var hist = Enumerable.Repeat(10m, 28).ToList();
        var policy = new EMaxESegPolicy();

        var p = policy.Compute(Ctx(hist, lt: 7, ciclo: 7));

        // s = μ·LT + safety = 10·7 + 0 = 70.
        p.ReorderPoint.Should().BeApproximately(70m, 0.001m);
        // S = μ·(LT+ciclo) + safety = 10·14 + 0 = 140.
        p.OrderUpToLevel.Should().BeApproximately(140m, 0.001m);
    }

    [Fact]
    public void Historico_com_desvio_aumenta_safety_stock_proporcionalmente_a_z_e_raiz_de_LT()
    {
        // 14 dias com 10, 14 dias com 20 → σ amostral ≈ 5.09.
        var hist = Enumerable.Repeat(10m, 14).Concat(Enumerable.Repeat(20m, 14)).ToList();
        var policyBaixo = new EMaxESegPolicy();
        var policyAlto = new EMaxESegPolicy();

        var p1 = policyBaixo.Compute(Ctx(hist, z: 1.0));
        var p2 = policyAlto.Compute(Ctx(hist, z: 2.0));

        // Dobrar z dobra o gap (s − μ·LT) entre as duas políticas.
        var safety1 = p1.ReorderPoint - 15m * 7m;
        var safety2 = p2.ReorderPoint - 15m * 7m;
        safety2.Should().BeApproximately(safety1 * 2, 0.001m);
        safety1.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Historico_vazio_resulta_em_s_S_iguais_a_zero()
    {
        var p = new EMaxESegPolicy().Compute(Ctx([]));

        p.ReorderPoint.Should().Be(0);
        p.OrderUpToLevel.Should().Be(0);
    }

    [Fact]
    public void JanelaDias_limita_o_historico_usado()
    {
        // 30 dias de 5, depois 5 dias de 100. JanelaDias=5 deve usar SÓ os últimos 5 (μ=100).
        var hist = Enumerable.Repeat(5m, 30).Concat(Enumerable.Repeat(100m, 5)).ToList();
        var policy = new EMaxESegPolicy { JanelaDias = 5 };

        var p = policy.Compute(Ctx(hist, lt: 7, ciclo: 7));

        // μ=100, σ=0 → s = 100·7 = 700.
        p.ReorderPoint.Should().Be(700m);
        p.OrderUpToLevel.Should().Be(1400m);
    }
}
