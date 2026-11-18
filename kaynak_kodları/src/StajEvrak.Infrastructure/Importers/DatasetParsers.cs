using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using StajEvrak.Application.Dataset;

namespace StajEvrak.Infrastructure.Importers;

/// <summary>Excel (.xlsx) veri seti okuyucu — ilk çalışma sayfasını kullanır.</summary>
public sealed class ExcelDatasetParser : DatasetParser
{
    public override string FormatName => "Excel";
    public override IReadOnlyCollection<string> SupportedExtensions => [".xlsx"];

    protected override IEnumerable<IReadOnlyList<string>> ReadRows(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var range = sheet.RangeUsed();
        if (range is null) yield break;

        var lastColumn = range.LastColumn().ColumnNumber();
        foreach (var row in range.Rows())
        {
            var cells = new List<string>(lastColumn);
            for (var c = 1; c <= lastColumn; c++)
                cells.Add(CellText(row.Cell(c)));
            yield return cells;
        }
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime().ToString("dd.MM.yyyy");
        if (cell.DataType == XLDataType.Number) return cell.GetDouble().ToString("0.##", CultureInfo.InvariantCulture);
        return cell.GetFormattedString().Trim();
    }
}

/// <summary>CSV veri seti okuyucu — ayraç (virgül / noktalı virgül) otomatik algılanır.</summary>
public sealed class CsvDatasetParser : DatasetParser
{
    public override string FormatName => "CSV";
    public override IReadOnlyCollection<string> SupportedExtensions => [".csv"];

    protected override IEnumerable<IReadOnlyList<string>> ReadRows(Stream content)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            DetectDelimiterValues = [";", ",", "\t"],
            BadDataFound = null,
            MissingFieldFound = null
        };
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var parser = new CsvParser(reader, config);
        while (parser.Read())
        {
            var record = parser.Record;
            if (record is not null) yield return record;
        }
    }
}
