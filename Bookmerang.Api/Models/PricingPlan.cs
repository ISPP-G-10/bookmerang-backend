using NpgsqlTypes;

namespace Bookmerang.Api.Models;

public enum PricingPlan
{
    [PgName("FREE")]
    Free,
    [PgName("PREMIUM")]
    Premium
}
