namespace CosmosPro.ML.DemandForCast.SyntheticData.Generation;

/// <summary>
/// Modelo determinístico de demanda diária por SKU/loja. Compõe (a) baseline ABC
/// por SKU, (b) sazonalidade semanal+anual, (c) janela de promoção, (d) ruído
/// Poisson, e (e) ruptura. Sub-classe pra cada componente facilita testar
/// individualmente quando virar feature engineering.
/// </summary>
internal sealed class DemandModel
{
    private readonly Random _rng;
    private readonly double[] _baselineAbc;
    private readonly double[] _promoStart;
    private readonly double[] _promoLen;
    private readonly bool[] _temPromocao;

    public DemandModel(int numSkus, int numLojas, DateOnly inicio, DateOnly fim, SyntheticDatasetOptions opt, int seed)
    {
        _rng = new Random(seed);
        NumSkus = numSkus;
        NumLojas = numLojas;
        Inicio = inicio;
        Fim = fim;
        Options = opt;
        DiasHorizonte = fim.DayNumber - inicio.DayNumber + 1;

        // ABC: power-law curve. baseline_i = c * (1/i)^alpha. Alpha = 1.2 dá
        // concentração ABC clássica (~80% volume nos top 20% SKUs); ajustes pra
        // baixo achatam a curva e desfazem o gradiente.
        _baselineAbc = new double[numSkus];
        const double alpha = 1.2;
        for (int i = 0; i < numSkus; i++)
        {
            var rank = i + 1;
            _baselineAbc[i] = 80.0 * Math.Pow(1.0 / rank, alpha) + 0.05;
        }

        // Promoção: escolhe ~5% dos SKUs e gera janela aleatória.
        _temPromocao = new bool[numSkus];
        _promoStart = new double[numSkus];
        _promoLen = new double[numSkus];
        int quantosComPromo = (int)Math.Round(numSkus * opt.FracaoSkusEmPromocao);
        var indicesComPromo = new HashSet<int>();
        while (indicesComPromo.Count < quantosComPromo)
        {
            indicesComPromo.Add(_rng.Next(numSkus));
        }
        foreach (var idx in indicesComPromo)
        {
            _temPromocao[idx] = true;
            _promoStart[idx] = _rng.Next(DiasHorizonte - 14);
            _promoLen[idx] = _rng.Next(7, 15);
        }
    }

    public int NumSkus { get; }
    public int NumLojas { get; }
    public DateOnly Inicio { get; }
    public DateOnly Fim { get; }
    public int DiasHorizonte { get; }
    public SyntheticDatasetOptions Options { get; }

    /// <summary>
    /// Retorna quantidade vendida no dia (≥ 0). Aplica baseline ABC × sazonalidade
    /// semanal × sazonalidade anual × promo × ruído Poisson. Não considera ruptura
    /// (chamador faz check antes).
    /// </summary>
    public int DemandaDia(int skuIndex, DateOnly data)
    {
        var baseline = _baselineAbc[skuIndex];
        var fatorSemanal = FatorSemanal(data.DayOfWeek);
        var fatorAnual = FatorAnual(data);
        var fatorPromo = FatorPromocao(skuIndex, data);

        var lambda = baseline * fatorSemanal * fatorAnual * fatorPromo;
        // Poisson sampling via Knuth (ok pra lambda < ~30; pra valores maiores
        // perde precisão mas o teto natural aqui é ~150 e o erro é aceitável).
        return SamplePoisson(lambda);
    }

    public bool EmPromocao(int skuIndex, DateOnly data) => FatorPromocao(skuIndex, data) > 1.0;

    public (DateOnly Inicio, DateOnly Fim, double Desconto)? PromocaoDoSku(int skuIndex)
    {
        if (!_temPromocao[skuIndex]) return null;
        var start = Inicio.AddDays((int)_promoStart[skuIndex]);
        var end = start.AddDays((int)_promoLen[skuIndex] - 1);
        // Desconto entre 10 e 30% — separado do multiplicador de venda.
        var desconto = 10.0 + _rng.NextDouble() * 20.0;
        return (start, end, desconto);
    }

    /// <summary>
    /// Probabilidade de ruptura por dia para um SKU dado seu rank ABC. Top sellers
    /// rupturam menos (operação cuida) — cauda rupturada com frequência.
    /// </summary>
    public bool EmRuptura(int skuIndex, DateOnly _data, Random rng)
    {
        var rank = skuIndex + 1;
        // Top 50 ~ 1.5% ; meio ~ 3% ; cauda > 8%.
        double prob = Options.RupturaBaseDiaria;
        if (rank > NumSkus * 0.7) prob *= 2.5;
        else if (rank <= 50) prob *= 0.5;
        return rng.NextDouble() < prob;
    }

    private static double FatorSemanal(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => 0.6,
        DayOfWeek.Saturday => 1.5,
        _ => 1.0,
    };

    private static double FatorAnual(DateOnly data)
    {
        // Curva senoidal com pico no inverno BR (julho ~ dia 200) — gripe/respiratório
        // puxam a demanda. Amplitude 15%.
        var diaNoAno = data.DayOfYear;
        var phase = (diaNoAno - 200) / 365.0 * 2 * Math.PI;
        return 1.0 + 0.15 * Math.Cos(phase);
    }

    private double FatorPromocao(int skuIndex, DateOnly data)
    {
        if (!_temPromocao[skuIndex]) return 1.0;
        var diaIdx = data.DayNumber - Inicio.DayNumber;
        if (diaIdx < _promoStart[skuIndex] || diaIdx >= _promoStart[skuIndex] + _promoLen[skuIndex])
            return 1.0;
        var (min, max) = Options.MultiplicadorPromocao;
        // Multiplicador fixo por SKU (não varia dia-a-dia dentro da janela).
        return min + (max - min) * Hash01(skuIndex);
    }

    private int SamplePoisson(double lambda)
    {
        if (lambda <= 0) return 0;
        // Para lambda muito alto, aproximação normal — evita loop longo.
        if (lambda > 30)
        {
            var z = SampleNormal();
            return Math.Max(0, (int)Math.Round(lambda + Math.Sqrt(lambda) * z));
        }
        double l = Math.Exp(-lambda);
        int k = 0;
        double p = 1.0;
        do
        {
            k++;
            p *= _rng.NextDouble();
        } while (p > l);
        return k - 1;
    }

    private double SampleNormal()
    {
        // Box-Muller.
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double Hash01(int x)
    {
        // 0..1 determinístico por x sem alocação.
        unchecked
        {
            var h = (uint)(x * 2654435761U);
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
