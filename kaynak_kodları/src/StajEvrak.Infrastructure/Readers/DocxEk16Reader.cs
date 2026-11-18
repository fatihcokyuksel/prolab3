using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using W14 = DocumentFormat.OpenXml.Office2010.Word;

namespace StajEvrak.Infrastructure.Readers;

/// <summary>
/// Word (.docx) Ek-16 okuyucu. Formdaki her satır "etiket | değer | etiket | değer" yapısındadır;
/// etiket hücresinin sağındaki hücre değer olarak alınır. Sağlık statüsü onay kutularından okunur.
/// </summary>
public sealed class DocxEk16Reader : DocumentReader
{
    public override IReadOnlyCollection<string> SupportedExtensions => [".docx"];

    protected override Dictionary<string, string?> Extract(string fullPath)
    {
        var result = new Dictionary<string, string?>();
        // Dosya başka bir programda (örn. Word) açık olsa da salt okunur okunabilsin.
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body ?? throw new InvalidDataException("Word belgesinin gövdesi bulunamadı.");

        foreach (var table in body.Descendants<Table>())
        {
            var rows = table.Elements<TableRow>().Select(r => r.Elements<TableCell>().ToList()).ToList();
            var section = Ek16LabelMatcher.DetectSection(rows.SelectMany(r => r).Select(CellText));
            if (section is null) continue;

            foreach (var cells in rows)
            {
                for (var i = 0; i < cells.Count - 1; i++)
                {
                    var fields = Ek16LabelMatcher.Match(CellText(cells[i]), section.Value);
                    if (fields.Count == 0) continue;
                    if (fields.Any(f => result.ContainsKey(f.Code))) continue; // ilk eşleşme geçerli

                    var valueCell = cells[i + 1];
                    var raw = fields[0].Kind == Domain.Ek16.FieldKind.Choice ? CheckboxText(valueCell) : CellText(valueCell);
                    Assign(result, fields, raw);
                    i++; // değer hücresini atla
                }
            }
        }
        return result;
    }

    /// <summary>Hücredeki paragrafları satır sonu ile birleştirir.</summary>
    private static string CellText(TableCell cell) =>
        string.Join("\n", cell.Elements<Paragraph>().Select(p => p.InnerText)).Trim();

    /// <summary>
    /// Onay kutularını (w14:checkbox) sırasıyla okuyup "1-☐ 2-☒ ..." biçiminde metne çevirir.
    /// Kutu yoksa hücre metni döner (ör. elle yazılmış "3").
    /// </summary>
    private static string CheckboxText(TableCell cell)
    {
        var boxes = cell.Descendants<W14.SdtContentCheckBox>().ToList();
        if (boxes.Count == 0) return CellText(cell);

        var parts = boxes.Select((box, index) =>
        {
            var val = box.Checked?.Val?.Value;
            var isChecked = val is not null && (val.Value == W14.OnOffValues.One || val.Value == W14.OnOffValues.True);
            return $"{index + 1}-{(isChecked ? "☒" : "☐")}";
        });
        return string.Join(" ", parts);
    }
}
