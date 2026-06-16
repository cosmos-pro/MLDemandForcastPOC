namespace CosmosPro.ML.DemandForCast.Purchasing.Simulation;

/// <summary>Resultado completo da simulação — uma entrada em <see cref="Politicas"/> por política avaliada.</summary>
public sealed record SimulationResult(
    DateOnly DataInicio,
    DateOnly DataFim,
    int LeadTimeDias,
    int CicloDias,
    double FatorServico,
    int SeriesAvaliadas,
    IReadOnlyList<PolicySimulationResult> Politicas);

/// <summary>KPIs agregados de uma política sobre toda a janela.</summary>
/// <param name="ListaCompraFinal">
/// Foto do último dia da janela: a decisão (s, S, qtd sugerida) de cada SKU×loja
/// como se o comprador abrisse a tela "hoje". É o artefato acionável da política.
/// </param>
/// <param name="Pedidos">
/// Livro de pedidos: todo pedido lançado pela política ao longo da janela
/// (auditoria). Cardinalidade ≈ <see cref="PolicyKpis.Pedidos"/>.
/// </param>
public sealed record PolicySimulationResult(
    string Policy,
    PolicyKpis Global,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, PolicyKpis>> PorDimensao,
    IReadOnlyList<BuyListItem> ListaCompraFinal,
    IReadOnlyList<OrderRecord> Pedidos);

/// <summary>
/// Uma linha da "lista de compra" — a decisão de reposição de um SKU×loja no
/// último dia simulado. <see cref="QuantidadeSugerida"/> só é &gt; 0 quando a
/// posição de estoque ficou ≤ <see cref="ReorderPoint"/> (gatilho de pedido).
/// </summary>
public sealed record BuyListItem(
    string Sku,
    int LojaId,
    decimal Estoque,
    decimal EmTransito,
    decimal Posicao,
    decimal ReorderPoint,
    decimal OrderUpToLevel,
    decimal QuantidadeSugerida);

/// <summary>Um pedido lançado durante o replay (livro de pedidos / auditoria).</summary>
public sealed record OrderRecord(
    DateOnly Data,
    string Sku,
    int LojaId,
    decimal Quantidade,
    decimal PosicaoAntes,
    decimal ReorderPoint,
    decimal OrderUpToLevel);

/// <summary>
/// KPIs de uma política em um agregado (global ou por dimensão).
/// Definições alinhadas com a literatura de inventory management.
/// </summary>
/// <param name="DemandaTotal">Soma da demanda real (unidades) no período.</param>
/// <param name="VendaRealizada">Demanda efetivamente atendida (≤ DemandaTotal).</param>
/// <param name="VendaPerdida">DemandaTotal − VendaRealizada (unidades).</param>
/// <param name="NivelServicoUnidades">VendaRealizada / DemandaTotal — *fill rate* (0..1).</param>
/// <param name="DiasComRuptura">Quantos dia-SKU-loja tiveram estoque insuficiente.</param>
/// <param name="DiasTotais">Dia-SKU-loja totais no período (para denominar nível de serviço por dia).</param>
/// <param name="NivelServicoDias">1 − DiasComRuptura / DiasTotais — *cycle service level*.</param>
/// <param name="EstoqueMedio">Estoque médio (média do estoque final do dia).</param>
/// <param name="CoberturaMediaDias">EstoqueMedio / (DemandaTotal / DiasTotais) — dias de cobertura.</param>
/// <param name="Giro">DemandaTotal / EstoqueMedio (turnover).</param>
/// <param name="Pedidos">Quantos pedidos foram disparados pela política.</param>
/// <param name="UnidadesPedidas">Soma das quantidades pedidas.</param>
/// <param name="CustoCarregamento">EstoqueMedio × DiasTotais × custo unitário/dia.</param>
/// <param name="CustoRuptura">VendaPerdida × custo unitário de ruptura.</param>
/// <param name="CustoTotal">Soma dos dois acima.</param>
public sealed record PolicyKpis(
    decimal DemandaTotal,
    decimal VendaRealizada,
    decimal VendaPerdida,
    double NivelServicoUnidades,
    int DiasComRuptura,
    int DiasTotais,
    double NivelServicoDias,
    decimal EstoqueMedio,
    double CoberturaMediaDias,
    double Giro,
    int Pedidos,
    decimal UnidadesPedidas,
    decimal CustoCarregamento,
    decimal CustoRuptura,
    decimal CustoTotal);
