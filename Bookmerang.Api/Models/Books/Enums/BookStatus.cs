using NpgsqlTypes;

namespace Bookmerang.Api.Models.Books.Enums;

public enum BookStatus
{
    [PgName("PUBLISHED")]
    Published,
    [PgName("DRAFT")]
    Draft,
    [PgName("PAUSED")]
    Paused,
    [PgName("RESERVED")]
    Reserved,
    [PgName("EXCHANGED")]
    Exchanged,
    [PgName("DELETED")]
    Deleted
}