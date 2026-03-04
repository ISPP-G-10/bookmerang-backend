using NpgsqlTypes;

namespace Bookmerang.Api.Models.Entities;

public enum PricingPlan
{
    [PgName("FREE")]
    Free,
    [PgName("PREMIUM")]
    Premium
}
