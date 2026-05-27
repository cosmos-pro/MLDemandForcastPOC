using System.Globalization;
using System.Text;

namespace CosmosPro.ML.DemandForCast.SyntheticData.Generation;

/// <summary>
/// Writer mínimo CSV que evita dependência de CsvHelper neste projeto. Usa
/// <see cref="CultureInfo.InvariantCulture"/> (ponto como separador decimal,
/// ISO 8601 para datas) — bate com a config do reader no Worker.
/// </summary>
internal sealed class CsvRowBuilder
{
    private readonly StringBuilder _sb = new();
    private bool _firstColumn = true;

    public CsvRowBuilder Add(string? value)
    {
        if (!_firstColumn) _sb.Append(',');
        _firstColumn = false;
        if (value is null) return this;
        if (value.IndexOfAny([',', '"', '\n', '\r']) >= 0)
        {
            _sb.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
        }
        else
        {
            _sb.Append(value);
        }
        return this;
    }

    public CsvRowBuilder Add(int value) => Add(value.ToString(CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(int? value) => Add(value?.ToString(CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(byte value) => Add(value.ToString(CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(bool value) => Add(value ? "1" : "0");
    public CsvRowBuilder Add(decimal value) => Add(value.ToString("0.####", CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(decimal? value) => Add(value?.ToString("0.####", CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(DateOnly value) => Add(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    public CsvRowBuilder Add(DateOnly? value) => Add(value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public string Build() => _sb.ToString();
}
