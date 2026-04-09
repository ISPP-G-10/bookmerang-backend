using Bookmerang.Api.Controllers.Admin;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class AdminExchangeControllerTests
{
	private readonly Mock<IExchangeService> _exchangeService = new();

	private AdminExchangeController CreateController() =>
		new(_exchangeService.Object);

	[Fact]
	public async Task GetAll_ExchangesExist_ReturnsOkWithList()
	{
		var controller = CreateController();

		_exchangeService
			.Setup(s => s.GetAllExchanges())
			.ReturnsAsync(new List<Exchange>
			{
				new() { ExchangeId = 70, ChatId = 70, MatchId = 70, Status = ExchangeStatus.NEGOTIATING, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
				new() { ExchangeId = 71, ChatId = 71, MatchId = 71, Status = ExchangeStatus.NEGOTIATING, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
			});

		var result = await controller.GetAll();

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.GetAllExchanges(), Times.Once);
	}

	[Fact]
	public async Task GetAll_NoExchanges_ReturnsNotFound()
	{
		var controller = CreateController();

		_exchangeService
			.Setup(s => s.GetAllExchanges())
			.ReturnsAsync(new List<Exchange>());

		var result = await controller.GetAll();

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task Delete_ExchangeExists_ReturnsNoContent()
	{
		var controller = CreateController();
		var exchange = new Exchange { ExchangeId = 42, ChatId = 42, MatchId = 42 };

		_exchangeService
			.Setup(s => s.GetExchangeById(42))
			.ReturnsAsync(exchange);
		_exchangeService
			.Setup(s => s.DeleteExchange(42))
			.ReturnsAsync(true);

		var result = await controller.Delete(42);

		Assert.IsType<NoContentResult>(result);
		_exchangeService.Verify(s => s.DeleteExchange(42), Times.Once);
	}

	[Fact]
	public async Task Delete_ExchangeDoesNotExist_ReturnsNotFound()
	{
		var controller = CreateController();

		_exchangeService
			.Setup(s => s.GetExchangeById(999))
			.ReturnsAsync((Exchange?)null);

		var result = await controller.Delete(999);

		Assert.IsType<NotFoundObjectResult>(result);
		_exchangeService.Verify(s => s.DeleteExchange(It.IsAny<int>()), Times.Never);
	}
}
