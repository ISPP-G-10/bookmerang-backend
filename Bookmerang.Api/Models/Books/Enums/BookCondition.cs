using NpgsqlTypes;

namespace Bookmerang.Api.Models.Books.Enums;

public enum BookCondition
{
    [PgName("LIKE_NEW")]
    LikeNew,
    [PgName("VERY_GOOD")]
    VeryGood,
    [PgName("GOOD")]
    Good,
    [PgName("ACCEPTABLE")]
    Acceptable,
    [PgName("POOR")]
    Poor
}