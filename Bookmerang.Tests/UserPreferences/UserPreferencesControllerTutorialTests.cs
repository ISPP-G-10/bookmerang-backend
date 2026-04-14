using Bookmerang.Api.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Bookmerang.Tests.UserPreferences;

public class UserPreferencesControllerTutorialTests
{
    private readonly Mock<IUserPreferenceService> _service = new();

    private UserPreferencesController CreateController() => new(_service.Object);

    [Fact]
    public async Task GetTutorialStatus_UserExists_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        _service.Setup(s => s.GetTutorialStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TutorialStatusDto { TutorialCompleted = true });

        var controller = CreateController();
        var result = await controller.GetTutorialStatus(userId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TutorialStatusDto>(ok.Value);
        Assert.True(dto.TutorialCompleted);
    }

    [Fact]
    public async Task GetTutorialStatus_UserMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        _service.Setup(s => s.GetTutorialStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TutorialStatusDto?)null);

        var controller = CreateController();
        var result = await controller.GetTutorialStatus(userId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SetTutorialStatus_UserExists_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateTutorialStatusDto { TutorialCompleted = true };
        _service.Setup(s => s.SetTutorialStatusAsync(userId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TutorialStatusDto { TutorialCompleted = true });

        var controller = CreateController();
        var result = await controller.SetTutorialStatus(userId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TutorialStatusDto>(ok.Value);
        Assert.True(dto.TutorialCompleted);
    }

    [Fact]
    public async Task SetTutorialStatus_UserMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateTutorialStatusDto { TutorialCompleted = true };
        _service.Setup(s => s.SetTutorialStatusAsync(userId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TutorialStatusDto?)null);

        var controller = CreateController();
        var result = await controller.SetTutorialStatus(userId, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
