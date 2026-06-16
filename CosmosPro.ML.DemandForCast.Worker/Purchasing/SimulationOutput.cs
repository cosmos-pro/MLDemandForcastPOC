using CosmosPro.ML.DemandForCast.Purchasing.Simulation;

namespace CosmosPro.ML.DemandForCast.Worker.Purchasing;

/// <summary>
/// Envelope persistido em <c>SimulacaoCompra.ResultadoJson</c>. Inclui o
/// <see cref="SimulationResult"/> bruto + metadados úteis para a UI (data de
/// geração, treino origem, nome dos produtos para a lista de compra).
/// </summary>
public sealed record SimulationOutput(
    DateTimeOffset GeradoEm,
    Guid TreinoJobId,
    IReadOnlyDictionary<string, string> Produtos,
    SimulationResult Resultado);
