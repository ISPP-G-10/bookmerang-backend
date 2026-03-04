using NpgsqlTypes;

namespace Bookmerang.Api.Models.Enums;

public enum PricingPlan
{
    [PgName("FREE")]
    Free,
    [PgName("PREMIUM")]
    Premium
}
