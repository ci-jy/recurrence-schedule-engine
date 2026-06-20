using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rse.Api.Data;

/// <summary>A stored recurring series. DTSTART and EXDATEs are local wall-clock times in <see cref="TimeZone"/>.</summary>
public class SeriesEntity
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(100)]
    public string? Resource { get; set; }

    [MaxLength(500)]
    public required string RRule { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime DtStart { get; set; }

    [MaxLength(64)]
    public required string TimeZone { get; set; }

    public int DurationMinutes { get; set; }

    [Column(TypeName = "timestamp without time zone[]")]
    public List<DateTime> ExDates { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
