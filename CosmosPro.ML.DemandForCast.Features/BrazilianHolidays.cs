namespace CosmosPro.ML.DemandForCast.Features;

/// <summary>
/// Feriados nacionais brasileiros. Cobre os fixos + móveis derivados da Páscoa
/// (Carnaval, Sexta-feira Santa, Corpus Christi). Não inclui feriados estaduais
/// /municipais — varejo farma tem demanda elevada em véspera de feriado, então
/// o flag é uma feature de calendário relevante.
/// </summary>
public static class BrazilianHolidays
{
    public static bool IsHoliday(DateOnly date) => GetHolidays(date.Year).Contains(date);

    private static readonly Dictionary<int, HashSet<DateOnly>> _cache = [];

    public static IReadOnlySet<DateOnly> GetHolidays(int year)
    {
        if (_cache.TryGetValue(year, out var cached)) return cached;

        var easter = ComputeEaster(year);
        var set = new HashSet<DateOnly>
        {
            new(year, 1, 1),    // Confraternização Universal
            new(year, 4, 21),   // Tiradentes
            new(year, 5, 1),    // Dia do Trabalho
            new(year, 9, 7),    // Independência
            new(year, 10, 12),  // Nossa Senhora Aparecida
            new(year, 11, 2),   // Finados
            new(year, 11, 15),  // Proclamação da República
            new(year, 12, 25),  // Natal
            easter.AddDays(-48),// Carnaval (segunda)
            easter.AddDays(-47),// Carnaval (terça)
            easter.AddDays(-2), // Sexta-feira Santa
            easter,             // Páscoa
            easter.AddDays(60), // Corpus Christi
        };
        _cache[year] = set;
        return set;
    }

    /// <summary>Algoritmo de Computus (Meeus/Jones/Butcher) para a Páscoa gregoriana.</summary>
    private static DateOnly ComputeEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}
