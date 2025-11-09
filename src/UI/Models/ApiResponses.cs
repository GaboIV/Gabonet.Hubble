namespace Gabonet.Hubble.UI.Models;

using Gabonet.Hubble.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Response model for paginated logs API
/// </summary>
public class LogsApiResponse
{
    /// <summary>
    /// List of logs for the current page
    /// </summary>
    public List<GeneralLog> Logs { get; set; } = new();

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of logs (before pagination)
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    public bool HasNextPage { get; set; }

    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Response model for log detail API
/// </summary>
public class LogDetailApiResponse
{
    /// <summary>
    /// Main log entry
    /// </summary>
    public GeneralLog? Log { get; set; }

    /// <summary>
    /// Related logs (e.g., application logs from the same request)
    /// </summary>
    public List<GeneralLog> RelatedLogs { get; set; } = new();

    /// <summary>
    /// Indicates if the log was found
    /// </summary>
    public bool Found { get; set; }
}

/// <summary>
/// Response model for configuration API
/// </summary>
public class ConfigApiResponse
{
    /// <summary>
    /// System statistics
    /// </summary>
    public HubbleStatistics? Statistics { get; set; }

    /// <summary>
    /// System configuration
    /// </summary>
    public HubbleSystemConfiguration? Configuration { get; set; }

    /// <summary>
    /// Hubble options
    /// </summary>
    public HubbleOptionsDto Options { get; set; } = new();
}

/// <summary>
/// DTO for Hubble options
/// </summary>
public class HubbleOptionsDto
{
    /// <summary>
    /// Base path for Hubble UI
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Prefix path for Hubble UI
    /// </summary>
    public string PrefixPath { get; set; } = string.Empty;

    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if HTTP request capture is enabled
    /// </summary>
    public bool CaptureHttpRequests { get; set; }

    /// <summary>
    /// Indicates if logger message capture is enabled
    /// </summary>
    public bool CaptureLoggerMessages { get; set; }

    /// <summary>
    /// List of ignored paths
    /// </summary>
    public List<string> IgnorePaths { get; set; } = new();

    /// <summary>
    /// Version information
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Generic API response for operations
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional data (optional)
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Response model for delete operation
/// </summary>
public class DeleteApiResponse : ApiResponse
{
    /// <summary>
    /// Number of deleted items
    /// </summary>
    public long DeletedCount { get; set; }
}

/// <summary>
/// Response model for prune operation
/// </summary>
public class PruneApiResponse : ApiResponse
{
    /// <summary>
    /// Number of pruned logs
    /// </summary>
    public long PrunedCount { get; set; }

    /// <summary>
    /// Cutoff date used for pruning
    /// </summary>
    public DateTime CutoffDate { get; set; }
}

/// <summary>
/// Request model for saving prune configuration
/// </summary>
public class SavePruneConfigRequest
{
    /// <summary>
    /// Enable automatic data pruning
    /// </summary>
    public bool EnableDataPrune { get; set; }

    /// <summary>
    /// Prune interval in hours
    /// </summary>
    public int DataPruneIntervalHours { get; set; }

    /// <summary>
    /// Maximum log age in hours
    /// </summary>
    public int MaxLogAgeHours { get; set; }
}

/// <summary>
/// Request model for saving capture configuration
/// </summary>
public class SaveCaptureConfigRequest
{
    /// <summary>
    /// Enable HTTP request capture
    /// </summary>
    public bool CaptureHttpRequests { get; set; }

    /// <summary>
    /// Enable logger message capture
    /// </summary>
    public bool CaptureLoggerMessages { get; set; }
}

/// <summary>
/// Request model for saving ignore paths
/// </summary>
public class SaveIgnorePathsRequest
{
    /// <summary>
    /// List of paths to ignore
    /// </summary>
    public List<string> IgnorePaths { get; set; } = new();
}
