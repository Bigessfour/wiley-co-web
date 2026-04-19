using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WileyCoWeb.Api;

internal sealed partial class WorkspaceReferenceDataImportService
{
    private static readonly Regex AccountCodeRegex = new(@"^\s*(?<code>\d+(?:\.\d+)?)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CityStateZipRegex = new(@"^(?<street>.*?)(?<city>[A-Za-z][A-Za-z .'-]+),\s*(?<state>[A-Za-z]{2})\s+(?<zip>\d{5}(?:-\d{4})?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^A-Za-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PersonNameTokenRegex = new(@"^[A-Za-z][A-Za-z'.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly IReadOnlyDictionary<string, Func<XElement, IReadOnlyList<string>, string>> CellValueReaders = new Dictionary<string, Func<XElement, IReadOnlyList<string>, string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["inlineStr"] = (cell, _) => ReadInlineStringCellValue(cell),
        ["s"] = ReadSharedStringValue
    };
}
