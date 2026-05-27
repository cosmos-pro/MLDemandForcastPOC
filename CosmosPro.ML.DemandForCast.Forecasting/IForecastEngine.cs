using CosmosPro.ML.DemandForCast.Features.Models;

namespace CosmosPro.ML.DemandForCast.Forecasting;

/// <summary>
/// Motor de previsão de demanda. Todos os engines (baselines e ML) operam sobre
/// os mesmos <see cref="FeatureVector"/> de F5 — o que muda é COMO derivam a
/// previsão. <see cref="Fit"/> recebe o conjunto de treino (linhas com target
/// válido) e devolve um modelo apto a prever.
///
/// <para>
/// Abstração presente desde o início (CLAUDE.md §1) para permitir trocar/comparar
/// engines e, no futuro, plugar um sidecar Python sem alterar o backtest.
/// </para>
/// </summary>
public interface IForecastEngine
{
    /// <summary>Nome curto para relatórios/comparação (ex.: "naive-sazonal", "lightgbm").</summary>
    string Name { get; }

    /// <summary>
    /// Ajusta o modelo ao conjunto de treino. Baselines podem ignorar (previsão
    /// vem direto das features); ML treina de fato. NÃO deve usar linhas com
    /// <see cref="FeatureVector.IsValidTarget"/> = false.
    /// </summary>
    IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet);
}

/// <summary>
/// Modelo treinado, capaz de prever a demanda de um dia-alvo a partir das suas
/// features (que respeitam o lead time — sem leakage, garantido por F5).
/// </summary>
public interface IForecastModel
{
    /// <summary>Demanda prevista (unidades, ≥ 0) para o dia-alvo descrito por <paramref name="features"/>.</summary>
    double Predict(FeatureVector features);
}
