namespace TagPrintStudio.Models;

public sealed class OutputProgress
{
    public string Operation { get; init; } = "";
    public int Current { get; init; }
    public int Total { get; init; }
    public string Detail { get; init; } = "";
}

public sealed class OutputPageException : Exception
{
    public string Operation { get; }
    public int PageNumber { get; }
    public int TotalPages { get; }

    public OutputPageException(string operation, int pageNumber, int totalPages, Exception innerException)
        : base(BuildMessage(operation, pageNumber, totalPages, innerException), innerException)
    {
        Operation = operation;
        PageNumber = pageNumber;
        TotalPages = totalPages;
    }

    private static string BuildMessage(string operation, int pageNumber, int totalPages, Exception innerException)
    {
        var totalText = totalPages > 0 ? totalPages.ToString() : "?";
        return $"{operation} failed on page {pageNumber}/{totalText}: {innerException.Message}";
    }
}
