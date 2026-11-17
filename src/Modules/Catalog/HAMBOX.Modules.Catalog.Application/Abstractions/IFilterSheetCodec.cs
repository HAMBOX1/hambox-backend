namespace HAMBOX.Modules.Catalog.Application.Abstractions;

/// <summary>A sheet as plain text: a header row and the rows under it (every cell is a string).</summary>
public sealed record FilterSheet(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>Reads and writes the Excel / CSV sheets used to manage filters in bulk.</summary>
public interface IFilterSheetCodec
{
    /// <summary>Parses an .xlsx or .csv upload (chosen by the file name) into a sheet. Throws <see cref="InvalidDataException"/> on a bad file.</summary>
    FilterSheet Read(Stream stream, string fileName);

    /// <summary>Writes a sheet as "xlsx" or "csv".</summary>
    byte[] Write(FilterSheet sheet, string format);
}
