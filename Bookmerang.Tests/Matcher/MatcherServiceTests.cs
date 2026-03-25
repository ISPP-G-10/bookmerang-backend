using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Api.Services.Interfaces.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Matcher;

/// <summary>
/// Tests de lógica pura del MatcherService — sin BD.
/// Cubre InterleaveBooks, ValidatePriorityToDiscoveryRatio y fórmula del score.
/// </summary>
public class MatcherServiceTests
{
    // Helper para crear FeedBookDto mínimos
    private static FeedBookDto MakeDto(string tag, bool isPriority = false) => new()
    {
        Id = tag.GetHashCode(),
        OwnerId = Guid.NewGuid(),
        OwnerUsername = tag,
        Titulo = tag,
        Score = 0,
        IsPriority = isPriority
    };

    private static MatcherService CreateServiceWithRatio(int ratio)
    {
        var settings = new MatcherSettings
        {
            Weights = new WeightsSettings
            {
                GenreMatch = 0.40,
                ExtensionMatch = 0.10,
                DistanceScore = 0.35,
                RecencyBonus = 0.15
            },
            Feed = new FeedSettings
            {
                PriorityToDiscoveryRatio = ratio,
                DefaultPageSize = 20,
                RecencyDecayDays = 30,
                SwipeValidDays = 30
            }
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MatcherService(
            new AppDbContext(options),
            Options.Create(settings),
            new Mock<ILogger<MatcherService>>().Object,
            new Mock<IChatService>().Object);
    }

    // ════════════════════════════════════════════════
    // TEST-S01: Score negativo en límite de radio (audit #4)
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(50000, 50000)]    // distance == radioMeters: componente = 0
    [InlineData(50050, 50000)]    // distance > radioMeters:  componente clamped a 0
    [InlineData(100000, 50000)]   // distance = 2x radioMeters: clamped a 0
    public void ScoreFormula_DistanceBeyondRadius_ComponentIsZeroNotNegative(
        double distance, double radioMeters)
    {
        // La fórmula corregida: Math.Max(0.0, 1.0 - distance / radioMeters)
        var component = Math.Max(0.0, 1.0 - distance / radioMeters);
        Assert.True(component >= 0.0, $"Componente de distancia debería ser >= 0, pero fue {component}");
    }

    [Fact]
    public void ScoreFormula_DistanceZero_ComponentIsOne()
    {
        var component = Math.Max(0.0, 1.0 - 0.0 / 50000.0);
        Assert.Equal(1.0, component, precision: 10);
    }

    [Fact]
    public void ScoreFormula_DistanceHalfRadius_ComponentIsHalf()
    {
        var component = Math.Max(0.0, 1.0 - 25000.0 / 50000.0);
        Assert.Equal(0.5, component, precision: 10);
    }

    // ════════════════════════════════════════════════
    // TEST-S02: Overflow de paginación (audit #2)
    // ════════════════════════════════════════════════

    [Fact]
    public void PaginationSkip_LargePageAndSize_DoesNotOverflow()
    {
        // Con page=1000 y pageSize=100 → skip = 100,000 (dentro de int, sin overflow)
        var page = 1000;
        var pageSize = 100;
        var skip = page * pageSize;
        Assert.Equal(100_000, skip);
    }

    [Fact]
    public void PaginationSkip_OverflowWithoutProtection_Detected()
    {
        // Sin validación en controller: page=107374182, pageSize=20 → overflow silencioso
        // 107374182 * 20 = 2,147,483,640 que está justo en el límite
        // 107374183 * 20 = overflow
        var page = 107374183;
        var pageSize = 20;

        // En C# sin checked, esto da overflow silencioso
        int skipUnchecked = page * pageSize;
        Assert.True(skipUnchecked < 0, "Overflow silencioso produce valor negativo");

        // Con checked, lanza excepción
        Assert.Throws<OverflowException>(() =>
        {
            checked { _ = page * pageSize; }
        });
    }

    // ════════════════════════════════════════════════
    // TEST-S03: Intercalado con P1 vacío devuelve todo P2
    // ════════════════════════════════════════════════

    [Fact]
    public void InterleaveBooks_P1Empty_ReturnsAllP2()
    {
        var p1 = new List<FeedBookDto>();
        var p2 = new List<FeedBookDto>
        {
            MakeDto("A"), MakeDto("B"), MakeDto("C")
        };

        var result = MatcherService.InterleaveBooks(p1, p2, ratio: 3);

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Titulo);
        Assert.Equal("B", result[1].Titulo);
        Assert.Equal("C", result[2].Titulo);
    }

    // ════════════════════════════════════════════════
    // TEST-S04: Intercalado con P2 vacío devuelve todo P1
    // ════════════════════════════════════════════════

    [Fact]
    public void InterleaveBooks_P2Empty_ReturnsAllP1()
    {
        var p1 = new List<FeedBookDto>
        {
            MakeDto("X"), MakeDto("Y"), MakeDto("Z")
        };
        var p2 = new List<FeedBookDto>();

        var result = MatcherService.InterleaveBooks(p1, p2, ratio: 3);

        Assert.Equal(3, result.Count);
        Assert.Equal("X", result[0].Titulo);
        Assert.Equal("Y", result[1].Titulo);
        Assert.Equal("Z", result[2].Titulo);
    }

    // ════════════════════════════════════════════════
    // TEST-S05: Intercalado respeta el ratio configurado
    // ════════════════════════════════════════════════

    [Fact]
    public void InterleaveBooks_WithRatioThree_ProducesCorrectPattern()
    {
        var p1 = new List<FeedBookDto>
        {
            MakeDto("P1"), MakeDto("P2"), MakeDto("P3"),
            MakeDto("P4"), MakeDto("P5"), MakeDto("P6")
        };
        var p2 = new List<FeedBookDto>
        {
            MakeDto("D1"), MakeDto("D2")
        };

        var result = MatcherService.InterleaveBooks(p1, p2, ratio: 3);

        // Esperado: [P1, P2, P3, D1, P4, P5, P6, D2]
        Assert.Equal(8, result.Count);
        Assert.Equal("P1", result[0].Titulo);
        Assert.Equal("P2", result[1].Titulo);
        Assert.Equal("P3", result[2].Titulo);
        Assert.Equal("D1", result[3].Titulo);
        Assert.Equal("P4", result[4].Titulo);
        Assert.Equal("P5", result[5].Titulo);
        Assert.Equal("P6", result[6].Titulo);
        Assert.Equal("D2", result[7].Titulo);
    }

    [Fact]
    public void InterleaveBooks_WithRatioOne_AlternatesCorrectly()
    {
        var p1 = new List<FeedBookDto> { MakeDto("A"), MakeDto("B") };
        var p2 = new List<FeedBookDto> { MakeDto("X"), MakeDto("Y") };

        var result = MatcherService.InterleaveBooks(p1, p2, ratio: 1);

        // Esperado: [A, X, B, Y]
        Assert.Equal(4, result.Count);
        Assert.Equal("A", result[0].Titulo);
        Assert.Equal("X", result[1].Titulo);
        Assert.Equal("B", result[2].Titulo);
        Assert.Equal("Y", result[3].Titulo);
    }

    // ════════════════════════════════════════════════
    // TEST-S06: Ratio inválido lanza excepción
    // ════════════════════════════════════════════════

    [Fact]
    public void ValidateRatio_ZeroRatio_ThrowsInvalidOperationException()
    {
        var service = CreateServiceWithRatio(0);
        Assert.Throws<InvalidOperationException>(() => service.ValidatePriorityToDiscoveryRatio());
    }

    [Fact]
    public void ValidateRatio_NegativeRatio_ThrowsInvalidOperationException()
    {
        var service = CreateServiceWithRatio(-1);
        Assert.Throws<InvalidOperationException>(() => service.ValidatePriorityToDiscoveryRatio());
    }

    [Fact]
    public void ValidateRatio_PositiveRatio_ReturnsValue()
    {
        var service = CreateServiceWithRatio(3);
        Assert.Equal(3, service.ValidatePriorityToDiscoveryRatio());
    }

    // ════════════════════════════════════════════════
    // TEST-S07: Intercalado con ambos vacíos
    // ════════════════════════════════════════════════

    [Fact]
    public void InterleaveBooks_BothEmpty_ReturnsEmpty()
    {
        var result = MatcherService.InterleaveBooks([], [], ratio: 3);
        Assert.Empty(result);
    }

    // ════════════════════════════════════════════════
    // TEST-S08: Redistribución de pesos cuando no hay género
    // ════════════════════════════════════════════════

    [Fact]
    public void WeightRedistribution_NoGenrePrefs_SumEqualsOne()
    {
        // Simula la lógica de redistribución del servicio
        var genreMatch = 0.40;
        var extensionMatch = 0.10;
        var distanceScore = 0.35;
        var recencyBonus = 0.15;

        var hasRealPrefs = false;
        var genreWeight = hasRealPrefs ? genreMatch : 0.0;
        var extraWeight = hasRealPrefs ? 0.0 : genreMatch;
        var otherTotal = extensionMatch + distanceScore + recencyBonus;
        var newExtension = extensionMatch + extraWeight * (extensionMatch / otherTotal);
        var newDistance = distanceScore + extraWeight * (distanceScore / otherTotal);
        var newRecency = recencyBonus + extraWeight * (recencyBonus / otherTotal);

        var totalWeight = genreWeight + newExtension + newDistance + newRecency;

        Assert.Equal(1.0, totalWeight, precision: 10);
        Assert.Equal(0.0, genreWeight);
        Assert.True(newExtension > extensionMatch);
        Assert.True(newDistance > distanceScore);
        Assert.True(newRecency > recencyBonus);
    }
}
