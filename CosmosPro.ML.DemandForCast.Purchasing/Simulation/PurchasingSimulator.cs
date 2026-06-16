using CosmosPro.ML.DemandForCast.Features.Models;

namespace CosmosPro.ML.DemandForCast.Purchasing.Simulation;

/// <summary>
/// Replay determinístico de compras: dado o histórico real de vendas
/// (<see cref="DailyObservation"/>) e o estoque inicial do dia anterior à janela,
/// simula dia-a-dia o que aconteceria se cada <see cref="IPurchasingPolicy"/>
/// fosse aplicada. Cada política é independente e parte do mesmo estado inicial
/// — comparação justa.
///
/// <para>
/// <b>Premissa de "demanda real"</b>: usamos a venda observada como proxy de
/// demanda. Em dias de ruptura no Stage, isso subestima a demanda real (a venda
/// foi 0 por falta de produto, não por falta de procura). Mascarar esses dias
/// inteiramente removeria o efeito mais importante a medir (ruptura sob a
/// política!), então fazemos uma substituição mínima: nos dias marcados como
/// ruptura, usamos a média histórica do SKU×loja como proxy. Heurística
/// pragmática — documentar isso no relatório do TCC.
/// </para>
///
/// <para>
/// Ordem de eventos dentro de um dia D:
/// 1) recebe pedidos cujo ArrivalDate = D (efeito na manhã);
/// 2) tenta atender a demanda (mínimo entre demanda e estoque);
/// 3) atualiza estoque, contabiliza venda perdida e ruptura;
/// 4) avalia política e dispara pedido se posição ≤ ROP;
/// 5) registra estoque final do dia para média móvel.
/// </para>
/// </summary>
public sealed class PurchasingSimulator
{
    public sealed record SerieKey(string Sku, int LojaId);

    /// <summary>Atributos de classificação por SKU×loja para drill-down (categoria, ABC, UF...).</summary>
    public sealed record SerieAttributes(string Sku, int LojaId, string Categoria, string ClasseAbc, string UF, string Regiao);

    public SimulationResult Run(
        SimulationOptions options,
        IReadOnlyList<DailyObservation> observations,
        IReadOnlyDictionary<SerieKey, decimal> estoqueInicial,
        IReadOnlyDictionary<SerieKey, SerieAttributes> atributos,
        IReadOnlyList<IPurchasingPolicy> policies,
        IForecaster? forecaster = null)
    {
        var bySerie = observations
            .GroupBy(o => new SerieKey(o.Sku, o.LojaId))
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Data).ToArray());

        // Demanda real diária por (chave, data). Em ruptura, substitui pela média histórica recente (não-ruptura).
        var demandaReal = BuildRealDemand(bySerie);

        var policyResults = new List<PolicySimulationResult>(policies.Count);
        foreach (var policy in policies)
        {
            var per = SimulateOnePolicy(options, policy, forecaster, bySerie, demandaReal, estoqueInicial, atributos);
            policyResults.Add(per);
        }

        return new SimulationResult(
            DataInicio: options.DataInicio,
            DataFim: options.DataFim,
            LeadTimeDias: options.LeadTimeDias,
            CicloDias: options.CicloDias,
            FatorServico: options.FatorServico,
            SeriesAvaliadas: bySerie.Count,
            Politicas: policyResults);
    }

    private static Dictionary<SerieKey, Dictionary<DateOnly, decimal>> BuildRealDemand(
        Dictionary<SerieKey, DailyObservation[]> bySerie)
    {
        var d = new Dictionary<SerieKey, Dictionary<DateOnly, decimal>>(bySerie.Count);
        foreach (var (key, serie) in bySerie)
        {
            // Média das observações não-ruptura (proxy para preencher dias com ruptura).
            var validas = serie.Where(o => !o.EmRuptura).Select(o => o.Quantidade).ToArray();
            var media = validas.Length == 0 ? 0m : validas.Average();

            var dict = new Dictionary<DateOnly, decimal>(serie.Length);
            foreach (var o in serie)
            {
                dict[o.Data] = o.EmRuptura ? media : o.Quantidade;
            }
            d[key] = dict;
        }
        return d;
    }

    private static PolicySimulationResult SimulateOnePolicy(
        SimulationOptions options,
        IPurchasingPolicy policy,
        IForecaster? forecaster,
        Dictionary<SerieKey, DailyObservation[]> bySerie,
        Dictionary<SerieKey, Dictionary<DateOnly, decimal>> demandaReal,
        IReadOnlyDictionary<SerieKey, decimal> estoqueInicial,
        IReadOnlyDictionary<SerieKey, SerieAttributes> atributos)
    {
        var perSerie = new Dictionary<SerieKey, SerieAccumulator>(bySerie.Count);
        foreach (var key in bySerie.Keys)
        {
            estoqueInicial.TryGetValue(key, out var inv);
            perSerie[key] = new SerieAccumulator(Math.Max(0, inv));
        }

        // Livro de pedidos (auditoria) e foto do último dia (lista de compra acionável).
        var pedidos = new List<OrderRecord>();
        var snapshot = new List<BuyListItem>(bySerie.Count);

        for (var d = options.DataInicio; d <= options.DataFim; d = d.AddDays(1))
        {
            var ultimoDia = d == options.DataFim;
            foreach (var (key, serie) in bySerie)
            {
                var acc = perSerie[key];

                // 1) recebe pedidos cujo dia de chegada é hoje.
                acc.ReceberPedidos(d);

                // 2) demanda real do dia (já tratada para ruptura).
                var demanda = demandaReal[key].GetValueOrDefault(d, 0m);

                // 3) tenta atender.
                var vendido = Math.Min(acc.Estoque, demanda);
                acc.Estoque -= vendido;
                acc.DemandaTotal += demanda;
                acc.VendaRealizada += vendido;
                var perdida = demanda - vendido;
                acc.VendaPerdida += perdida;
                if (perdida > 0) acc.DiasComRuptura++;
                acc.DiasTotais++;
                acc.EstoqueSomaDiaria += acc.Estoque;

                // 4) política avalia.
                var ctx = BuildContext(options, key, d, serie, acc, forecaster);
                var p = policy.Compute(ctx);
                var posicao = ctx.EstoqueAtual + ctx.EmTransito;
                var qtySugerida = posicao <= p.ReorderPoint ? Math.Max(0, p.OrderUpToLevel - posicao) : 0m;

                if (qtySugerida > 0)
                {
                    acc.LancarPedido(d.AddDays(options.LeadTimeDias), qtySugerida);
                    acc.Pedidos++;
                    acc.UnidadesPedidas += qtySugerida;
                    pedidos.Add(new OrderRecord(d, key.Sku, key.LojaId, qtySugerida, posicao, p.ReorderPoint, p.OrderUpToLevel));
                }

                // Foto do último dia: a decisão (s, S, qtd) de cada SKU×loja "hoje".
                if (ultimoDia)
                {
                    snapshot.Add(new BuyListItem(
                        key.Sku, key.LojaId,
                        ctx.EstoqueAtual, ctx.EmTransito, posicao,
                        p.ReorderPoint, p.OrderUpToLevel, qtySugerida));
                }
            }
        }

        // Agrega KPIs.
        var globalKpi = AggregateKpis(perSerie.Values, options);
        var porDim = AggregatePorDimensao(perSerie, atributos, options);

        return new PolicySimulationResult(policy.Name, globalKpi, porDim, snapshot, pedidos);
    }

    private static PolicyContext BuildContext(
        SimulationOptions options,
        SerieKey key,
        DateOnly hoje,
        DailyObservation[] serie,
        SerieAccumulator acc,
        IForecaster? forecaster)
    {
        // Histórico: últimas N observações até hoje-1, ignorando ruptura.
        var hist = new List<decimal>(options.JanelaHistoricoDias);
        for (int i = serie.Length - 1; i >= 0 && hist.Count < options.JanelaHistoricoDias; i--)
        {
            var o = serie[i];
            if (o.Data >= hoje) continue;
            if (o.EmRuptura) continue;
            hist.Add(o.Quantidade);
        }
        hist.Reverse();

        IReadOnlyList<decimal>? forecastFuturo = null;
        IReadOnlyList<decimal>? residuos = null;
        if (forecaster is not null)
        {
            var span = options.LeadTimeDias + options.CicloDias;
            var fc = new decimal[span];
            for (int i = 0; i < span; i++)
                fc[i] = forecaster.Predict(key.Sku, key.LojaId, hoje.AddDays(i + 1));
            forecastFuturo = fc;

            // Resíduos: para cada dia válido entre [hoje-LT-JanelaResiduos, hoje-LT-1],
            // (real − previsto). Janela termina em hoje-LT para garantir que o "real"
            // estaria conhecido no momento da decisão simulada.
            var res = new List<decimal>(options.JanelaResiduosDias);
            var fimRes = hoje.AddDays(-options.LeadTimeDias);
            var iniRes = fimRes.AddDays(-options.JanelaResiduosDias);
            for (int i = serie.Length - 1; i >= 0; i--)
            {
                var o = serie[i];
                if (o.Data >= fimRes) continue;
                if (o.Data < iniRes) break;
                if (o.EmRuptura) continue;
                var pred = forecaster.Predict(o.Sku, o.LojaId, o.Data);
                res.Add(o.Quantidade - pred);
            }
            residuos = res;
        }

        return new PolicyContext
        {
            Sku = key.Sku,
            LojaId = key.LojaId,
            Hoje = hoje,
            LeadTimeDias = options.LeadTimeDias,
            CicloDias = options.CicloDias,
            FatorServico = options.FatorServico,
            EstoqueAtual = acc.Estoque,
            EmTransito = acc.EmTransitoTotal(),
            HistoricoVendas = hist,
            ForecastFuturo = forecastFuturo,
            ResiduosForecast = residuos,
        };
    }

    private static PolicyKpis AggregateKpis(IEnumerable<SerieAccumulator> accs, SimulationOptions options)
    {
        decimal dem = 0, vend = 0, perd = 0, somaEstoque = 0, unidPed = 0;
        int dias = 0, rupt = 0, ped = 0;
        foreach (var a in accs)
        {
            dem += a.DemandaTotal;
            vend += a.VendaRealizada;
            perd += a.VendaPerdida;
            somaEstoque += a.EstoqueSomaDiaria;
            unidPed += a.UnidadesPedidas;
            dias += a.DiasTotais;
            rupt += a.DiasComRuptura;
            ped += a.Pedidos;
        }
        return ComputeKpis(dem, vend, perd, somaEstoque, unidPed, dias, rupt, ped, options);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, PolicyKpis>> AggregatePorDimensao(
        Dictionary<SerieKey, SerieAccumulator> perSerie,
        IReadOnlyDictionary<SerieKey, SerieAttributes> atributos,
        SimulationOptions options)
    {
        var dimensions = new (string Name, Func<SerieAttributes, string> Sel)[]
        {
            ("Categoria", a => a.Categoria),
            ("ClasseAbc", a => a.ClasseAbc),
            ("Loja", a => a.LojaId.ToString()),
            ("UF", a => a.UF),
        };

        var result = new Dictionary<string, IReadOnlyDictionary<string, PolicyKpis>>(dimensions.Length);
        foreach (var (dimName, sel) in dimensions)
        {
            var bucket = new Dictionary<string, List<SerieAccumulator>>();
            foreach (var (key, acc) in perSerie)
            {
                if (!atributos.TryGetValue(key, out var attr)) continue;
                var k = sel(attr);
                if (string.IsNullOrEmpty(k)) continue;
                if (!bucket.TryGetValue(k, out var list)) { list = []; bucket[k] = list; }
                list.Add(acc);
            }
            result[dimName] = bucket.ToDictionary(kv => kv.Key, kv => AggregateKpis(kv.Value, options));
        }
        return result;
    }

    private static PolicyKpis ComputeKpis(
        decimal dem, decimal vend, decimal perd, decimal somaEstoque, decimal unidPed,
        int dias, int rupt, int ped, SimulationOptions options)
    {
        var estoqueMedio = dias == 0 ? 0m : somaEstoque / dias;
        var demandaDiaria = dias == 0 ? 0.0 : (double)dem / dias;
        var coberturaDias = demandaDiaria <= 0 ? 0.0 : (double)estoqueMedio / demandaDiaria;
        var giro = estoqueMedio <= 0 ? 0.0 : (double)dem / (double)estoqueMedio;
        var nsUnid = dem <= 0 ? 1.0 : (double)vend / (double)dem;
        var nsDia = dias == 0 ? 1.0 : 1.0 - (double)rupt / dias;
        var custoCarreg = somaEstoque * options.CustoCarregamentoDia;
        var custoRupt = perd * options.CustoRupturaUnidade;

        return new PolicyKpis(
            DemandaTotal: dem,
            VendaRealizada: vend,
            VendaPerdida: perd,
            NivelServicoUnidades: nsUnid,
            DiasComRuptura: rupt,
            DiasTotais: dias,
            NivelServicoDias: nsDia,
            EstoqueMedio: estoqueMedio,
            CoberturaMediaDias: coberturaDias,
            Giro: giro,
            Pedidos: ped,
            UnidadesPedidas: unidPed,
            CustoCarregamento: custoCarreg,
            CustoRuptura: custoRupt,
            CustoTotal: custoCarreg + custoRupt);
    }

    private sealed class SerieAccumulator(decimal estoqueInicial)
    {
        public decimal Estoque = estoqueInicial;
        public decimal DemandaTotal;
        public decimal VendaRealizada;
        public decimal VendaPerdida;
        public decimal EstoqueSomaDiaria;
        public decimal UnidadesPedidas;
        public int DiasTotais;
        public int DiasComRuptura;
        public int Pedidos;

        private readonly SortedDictionary<DateOnly, decimal> _emTransito = new();

        public void LancarPedido(DateOnly chegada, decimal qty)
        {
            if (_emTransito.TryGetValue(chegada, out var cur))
                _emTransito[chegada] = cur + qty;
            else
                _emTransito[chegada] = qty;
        }

        public void ReceberPedidos(DateOnly hoje)
        {
            while (_emTransito.Count > 0)
            {
                var primeiro = _emTransito.First();
                if (primeiro.Key > hoje) break;
                Estoque += primeiro.Value;
                _emTransito.Remove(primeiro.Key);
            }
        }

        public decimal EmTransitoTotal()
        {
            decimal s = 0;
            foreach (var v in _emTransito.Values) s += v;
            return s;
        }
    }
}
