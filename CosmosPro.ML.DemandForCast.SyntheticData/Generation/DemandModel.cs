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

    // Drivers exógenos regionais (por UF, indexados por offset de dia a partir de Inicio).
    // _gripe ∈ ~[0, 1.2] (0 = baixa temporada, picos = surto); _climaTemp em °C.
    private readonly Dictionary<string, double[]> _gripe = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double[]> _climaTemp = new(StringComparer.OrdinalIgnoreCase);

    public DemandModel(int numSkus, int numLojas, DateOnly inicio, DateOnly fim, SyntheticDatasetOptions opt, int seed, IReadOnlyList<string> ufs)
    {
        _rng = new Random(seed);
        NumSkus = numSkus;
        NumLojas = numLojas;
        Inicio = inicio;
        Fim = fim;
        Options = opt;
        DiasHorizonte = fim.DayNumber - inicio.DayNumber + 1;
        Ufs = ufs;

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

        // Drivers regionais — RNG SEPARADO (não toca o stream de _rng acima, pra
        // não mudar a seleção de promoções e preservar a determinística existente).
        BuildDrivers(ufs, new Random(seed + 101));
    }

    public IReadOnlyList<string> Ufs { get; }

    public int NumSkus { get; }
    public int NumLojas { get; }
    public DateOnly Inicio { get; }
    public DateOnly Fim { get; }
    public int DiasHorizonte { get; }
    public SyntheticDatasetOptions Options { get; }

    /// <summary>
    /// Retorna quantidade vendida no dia (≥ 0). Aplica baseline ABC × sazonalidade
    /// semanal × sazonalidade anual genérica × <b>drivers exógenos regionais</b>
    /// (gripe/clima, com sensibilidade por subcategoria) × promo × ruído Poisson.
    /// Não considera ruptura (chamador faz check antes).
    ///
    /// <para>
    /// O efeito dos drivers inclui a parte sazonal E a <b>anomalia</b> (surto/onda
    /// de calor) — esta última NÃO é derivável do calendário, e é o que torna o
    /// sinal exógeno valioso como feature (ver Docs/08).
    /// </para>
    /// </summary>
    public int DemandaDia(int skuIndex, DateOnly data, string uf, string subcategoria)
    {
        var baseline = _baselineAbc[skuIndex];
        var fatorSemanal = FatorSemanal(data.DayOfWeek);
        var fatorAnual = FatorAnualGenerico(data);
        var fatorPromo = FatorPromocao(skuIndex, data);
        var fatorSinais = FatorSinais(uf, data, subcategoria);

        var lambda = baseline * fatorSemanal * fatorAnual * fatorSinais * fatorPromo;
        // Poisson sampling via Knuth (ok pra lambda < ~30; pra valores maiores
        // perde precisão mas o teto natural aqui é ~150 e o erro é aceitável).
        return SamplePoisson(lambda);
    }

    /// <summary>Índice de incidência de gripe (0..~120) na UF no dia — para emissão em SinaisExternos.</summary>
    public double GripeIndice(string uf, DateOnly data) =>
        Math.Round(GripeBruto(uf, data) * 100.0, 2);

    /// <summary>Temperatura (°C) na UF no dia — para emissão em SinaisExternos.</summary>
    public double ClimaTempC(string uf, DateOnly data)
    {
        var idx = DiaIdx(data);
        return _climaTemp.TryGetValue(uf, out var arr) && idx >= 0 && idx < arr.Length
            ? Math.Round(arr[idx], 1) : 0;
    }

    /// <summary>
    /// Sensibilidade de uma subcategoria aos drivers (gripe, calor). Define quais
    /// famílias de produto reagem — antitérmico/respiratório/antibiótico à gripe;
    /// antialérgico/vitamina ao calor. Demais ≈ 0.
    /// </summary>
    public static (double Gripe, double Calor) Sensibilidades(string? subcategoria) => subcategoria switch
    {
        "Respiratório" => (1.4, 0.0),
        "Antibióticos" => (0.9, 0.0),
        "Analgésicos" => (0.8, 0.0),      // antitérmico sobe na gripe
        "Anti-inflamatórios" => (0.6, 0.0),
        "Antialérgicos" => (0.0, 0.9),    // rinite no tempo seco/quente
        "Vitaminas" => (0.0, 0.5),
        "Gástricos" => (0.0, 0.2),
        _ => (0.0, 0.0),
    };

    private double FatorSinais(string uf, DateOnly data, string subcategoria)
    {
        var (sensG, sensC) = Sensibilidades(subcategoria);
        if (sensG == 0 && sensC == 0) return 1.0;

        var gripe = GripeBruto(uf, data);          // ~[0, 1.2], baseline de temporada ~0.3
        var calor = CalorIndice(uf, data);         // [0, 1], normalizado da temperatura
        // Centrado na temporada/clima "normal" — o ganho vem do desvio (anomalia).
        var fator = 1.0 + sensG * (gripe - 0.3) + sensC * (calor - 0.5);
        return Math.Max(0.1, fator);
    }

    private double GripeBruto(string uf, DateOnly data)
    {
        var idx = DiaIdx(data);
        return _gripe.TryGetValue(uf, out var arr) && idx >= 0 && idx < arr.Length ? arr[idx] : 0.3;
    }

    private double CalorIndice(string uf, DateOnly data)
    {
        var t = ClimaTempC(uf, data);
        return Math.Clamp((t - 15.0) / 25.0, 0.0, 1.0);  // 15°C→0, 40°C→1
    }

    private int DiaIdx(DateOnly data) => data.DayNumber - Inicio.DayNumber;

    /// <summary>
    /// Constrói as séries diárias de gripe e clima por UF: média sazonal (fase
    /// regional) + anomalia estocástica (surtos / ondas de calor) + ruído. A
    /// anomalia é o que o calendário não prevê.
    /// </summary>
    private void BuildDrivers(IReadOnlyList<string> ufs, Random rng)
    {
        foreach (var uf in ufs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var gripe = new double[DiasHorizonte];
            var clima = new double[DiasHorizonte];

            // Fase regional: UFs mais ao norte têm gripe mais fraca/deslocada e
            // clima mais quente. Usamos um deslocamento determinístico por UF + rng.
            var faseGripe = rng.NextDouble() * 20 - 10;       // ± 10 dias
            var tempBase = TempBaseUf(uf) + (rng.NextDouble() * 2 - 1);
            var ampSurtoUf = 0.3 + rng.NextDouble() * 0.3;    // intensidade máx do surto

            // Surtos de gripe: 1-2 por ano, centrados perto do inverno, largura variável.
            var surtos = new List<(double centro, double sigma, double altura)>();
            int nSurtos = 1 + (rng.NextDouble() < 0.5 ? 1 : 0);
            for (int k = 0; k < nSurtos; k++)
            {
                // Centro perto do dia-do-ano ~180 (inverno BR), ± algumas semanas.
                var centroDoY = 180 + (rng.NextDouble() * 60 - 30) + faseGripe;
                var sigma = 7 + rng.NextDouble() * 12;
                var altura = ampSurtoUf * (0.6 + rng.NextDouble() * 0.6);
                surtos.Add((centroDoY, sigma, altura));
            }

            // Ondas de calor: 1-3 por ano no verão.
            var ondas = new List<(double centro, double sigma, double altura)>();
            int nOndas = 1 + (int)(rng.NextDouble() * 3);
            for (int k = 0; k < nOndas; k++)
            {
                var centroDoY = (rng.NextDouble() < 0.5 ? 15 : 350) + (rng.NextDouble() * 30 - 15);
                var sigma = 5 + rng.NextDouble() * 8;
                var altura = 3 + rng.NextDouble() * 5;        // +3..8 °C
                ondas.Add((centroDoY, sigma, altura));
            }

            for (int i = 0; i < DiasHorizonte; i++)
            {
                var data = Inicio.AddDays(i);
                var doy = data.DayOfYear;

                // Gripe: base sazonal (pico inverno ~ dia 180) + surtos + ruído.
                var sazonal = 0.3 + 0.18 * Math.Cos((doy - 180 - faseGripe) / 365.0 * 2 * Math.PI);
                double surto = 0;
                foreach (var (centro, sigma, altura) in surtos)
                    surto += altura * GaussBump(doy, centro, sigma);
                var g = sazonal + surto + (rng.NextDouble() * 0.04 - 0.02);
                gripe[i] = Math.Clamp(g, 0.0, 1.2);

                // Clima: base UF + sazonal (pico verão, oposto à gripe) + ondas + ruído.
                var sazonalTemp = tempBase + 5.0 * Math.Cos((doy - 15) / 365.0 * 2 * Math.PI);
                double onda = 0;
                foreach (var (centro, sigma, altura) in ondas)
                    onda += altura * GaussBump(doy, centro, sigma);
                clima[i] = sazonalTemp + onda + (rng.NextDouble() * 1.5 - 0.75);
            }

            _gripe[uf] = gripe;
            _climaTemp[uf] = clima;
        }
    }

    /// <summary>Bump gaussiano de pico 1 em <paramref name="centro"/> (dia-do-ano, com wrap de 365).</summary>
    private static double GaussBump(int doy, double centro, double sigma)
    {
        var d = Math.Abs(doy - centro);
        d = Math.Min(d, 365 - d);   // distância circular no ano
        return Math.Exp(-(d * d) / (2 * sigma * sigma));
    }

    private static double TempBaseUf(string uf) => uf switch
    {
        // Norte/Nordeste quentes; Sul ameno.
        "AM" or "PA" or "AC" or "RO" or "RR" or "AP" or "TO" or "MA" => 30,
        "CE" or "RN" or "PB" or "PE" or "AL" or "SE" or "BA" or "PI" => 29,
        "MT" or "MS" or "GO" or "DF" => 27,
        "SP" or "RJ" or "ES" or "MG" => 24,
        "PR" or "SC" or "RS" => 20,
        _ => 25,
    };

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

    private static double FatorAnualGenerico(DateOnly data)
    {
        // Sazonalidade anual GENÉRICA leve (±5%) que afeta todos os SKUs. A
        // sazonalidade forte (gripe/calor) agora vem dos drivers regionais por
        // subcategoria sensível — ver FatorSinais.
        var diaNoAno = data.DayOfYear;
        var phase = (diaNoAno - 200) / 365.0 * 2 * Math.PI;
        return 1.0 + 0.05 * Math.Cos(phase);
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
