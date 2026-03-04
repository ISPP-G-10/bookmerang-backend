using NpgsqlTypes;

namespace Bookmerang.Api.Models.Entities;

public enum CoverType
{
    [PgName("HARDCOVER")]
    Hardcover,
    [PgName("PAPERBACK")]
    Paperback
}