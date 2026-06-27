using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Contracts;

/// <summary>Optional paging parameters for list endpoints.</summary>
public sealed class PaginationParameters
{
    /// <summary>1-based page number.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than or equal to 1.")]
    public int Page { get; init; } = 1;

    /// <summary>Number of items per page (1–100).</summary>
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = 20;
}
