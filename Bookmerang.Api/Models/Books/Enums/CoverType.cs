using NpgsqlTypes;

namespace Bookmerang.Api.Models.Books.Enums;

public enum CoverType
{
    [PgName("HARDCOVER")]
    Hardcover,
    [PgName("PAPERBACK")]
    Paperback
}