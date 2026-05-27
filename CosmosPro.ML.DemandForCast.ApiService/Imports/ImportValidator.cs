using System.IO.Compression;

namespace CosmosPro.ML.DemandForCast.ApiService.Imports;

internal static class ImportValidator
{
    /// <summary>
    /// Valida superficialmente: arquivos esperados estão presentes no ZIP e
    /// cada CSV tem todas as colunas obrigatórias no header (case-insensitive,
    /// qualquer ordem). Validação profunda (FKs, datas plausíveis, integridade
    /// cronológica) é responsabilidade do Worker dentro da transação.
    /// </summary>
    /// <param name="zipStream">Stream do arquivo .zip. Não é fechada pelo método.</param>
    public static ImportValidationResult Validate(Stream zipStream)
    {
        var errors = new List<string>();

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException or EndOfStreamException or IOException)
        {
            // ZipArchive joga diferentes exceptions dependendo de como o stream
            // diverge do formato ZIP — streams muito curtos lançam
            // ArgumentOutOfRangeException no seek de fim-de-central-directory.
            return new ImportValidationResult(false, [$"Arquivo não é um ZIP válido: {ex.Message}"]);
        }

        using (archive)
        {
            var entriesByName = archive.Entries
                .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var (expectedName, expectedColumns) in ImportSchemas.ExpectedFiles)
            {
                if (!entriesByName.TryGetValue(expectedName, out var entry))
                {
                    errors.Add($"Arquivo obrigatório ausente no ZIP: '{expectedName}'.");
                    continue;
                }

                var missingColumns = ReadMissingColumns(entry, expectedColumns);
                if (missingColumns.Count > 0)
                {
                    errors.Add($"'{expectedName}' está sem as colunas: {string.Join(", ", missingColumns)}.");
                }
            }
        }

        return new ImportValidationResult(errors.Count == 0, errors);
    }

    private static List<string> ReadMissingColumns(ZipArchiveEntry entry, string[] expectedColumns)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var header = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(header))
        {
            return ["(arquivo vazio ou sem header)"];
        }

        // Aceita vírgula ou ponto-e-vírgula como separador (Excel BR exporta com `;`).
        var sep = header.Contains(';') ? ';' : ',';
        var headerColumns = header.Split(sep)
            .Select(c => c.Trim().Trim('"').TrimStart('﻿')) // remove BOM em primeira coluna
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. expectedColumns.Where(c => !headerColumns.Contains(c))];
    }
}

internal sealed record ImportValidationResult(bool IsValid, IReadOnlyList<string> Errors);
