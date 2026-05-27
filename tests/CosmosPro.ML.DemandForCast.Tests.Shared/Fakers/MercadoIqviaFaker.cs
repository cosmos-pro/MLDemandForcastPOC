using Bogus;

namespace CosmosPro.ML.DemandForCast.Tests.Shared.Fakers;

public sealed record MercadoIqviaRow(
    DateOnly Mes,
    string PrincipioAtivo,
    string UF,
    decimal DemandaMercadoUnidades,
    decimal? MarketShareCategoria);

public sealed class MercadoIqviaFaker : Faker<MercadoIqviaRow>
{
    public MercadoIqviaFaker(IList<string> principiosAtivos, IList<string> ufs, DateOnly mesInicio, DateOnly mesFim, int seed = 42)
    {
        UseSeed(seed);
        CustomInstantiator(f =>
        {
            var startTotalMonths = mesInicio.Year * 12 + mesInicio.Month;
            var endTotalMonths = mesFim.Year * 12 + mesFim.Month;
            var pickedTotalMonths = f.Random.Int(startTotalMonths, endTotalMonths);
            var year = pickedTotalMonths / 12;
            var month = pickedTotalMonths % 12;
            if (month == 0) { month = 12; year--; }
            var mes = new DateOnly(year, month, 1);
            return new MercadoIqviaRow(
                Mes: mes,
                PrincipioAtivo: f.Random.ListItem(principiosAtivos),
                UF: f.Random.ListItem(ufs),
                DemandaMercadoUnidades: Math.Round((decimal)f.Random.Double(10_000, 5_000_000), 3),
                MarketShareCategoria: f.Random.Bool(0.8f)
                    ? Math.Round((decimal)f.Random.Double(0.01, 0.85), 4)
                    : null);
        });
    }
}
