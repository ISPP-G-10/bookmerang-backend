using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeServiceTests : IAsyncLifetime
{
	private AppDbContext _db = null!;
	private ExchangeService _service = null!;

	public Task InitializeAsync()
	{
		_db = DbContextFactory.CreateInMemory();
		_service = new ExchangeService(_db);
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _db.Database.EnsureDeletedAsync();
		await _db.DisposeAsync();
	}

	[Fact]
	public async Task CreateExchange_MatchDoesNotExist_ThrowsInvalidOperationException()
	{
		var chatId = Guid.NewGuid();
		_db.Chats.Add(new Chat { Id = chatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow });
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateExchange(chatId, 999));

		Assert.Contains("Match con id 999 no existe", ex.Message);
	}

	[Fact]
	public async Task CreateExchange_ChatDoesNotExist_ThrowsInvalidOperationException()
	{
		var chatId = Guid.NewGuid();
		_db.Matches.Add(new Match
		{
			Id = 44,
			User1Id = Guid.NewGuid(),
			User2Id = Guid.NewGuid(),
			Book1Id = 1,
			Book2Id = 2,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		});
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateExchange(chatId, 44));

		Assert.Contains($"Chat con id {chatId} no existe", ex.Message);
	}

	[Fact]
	public async Task CreateExchange_ValidChatAndMatch_CreatesExchangeInNegotiating()
	{
		var chatId = Guid.NewGuid();
		_db.Chats.Add(new Chat { Id = chatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow });
		_db.Matches.Add(new Match
		{
			Id = 55,
			User1Id = Guid.NewGuid(),
			User2Id = Guid.NewGuid(),
			Book1Id = 1,
			Book2Id = 2,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		});
		await _db.SaveChangesAsync();

		var created = await _service.CreateExchange(chatId, 55);

		Assert.True(created.ExchangeId > 0);
		Assert.Equal(chatId, created.ChatId);
		Assert.Equal(55, created.MatchId);
		Assert.Equal(ExchangeStatus.NEGOTIATING, created.Status);
		Assert.Equal(1, await _db.Exchanges.CountAsync());
	}

	[Fact]
	public async Task UpdateExchangeStatus_FromNegotiatingToAccepted_UpdatesStatus()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 21,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow.AddMinutes(-20),
			UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
		};

		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var previousUpdatedAt = exchange.UpdatedAt;

		var updated = await _service.UpdateExchangeStatus(exchange.ExchangeId, ExchangeStatus.ACCEPTED);

		Assert.Equal(ExchangeStatus.ACCEPTED, updated.Status);
		Assert.True(updated.UpdatedAt >= previousUpdatedAt);
	}

	[Fact]
	public async Task UpdateExchangeStatus_AlreadyRejected_ThrowsInvalidOperationException()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 32,
			Status = ExchangeStatus.REJECTED,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.UpdateExchangeStatus(exchange.ExchangeId, ExchangeStatus.ACCEPTED));

		Assert.Contains("No se puede modificar un intercambio ya rechazado", ex.Message);
	}

	// GET TESTS
	[Fact]
	public async Task GetExchangeById_ExchangeExists_ReturnsExchange()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 51,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var result = await _service.GetExchangeById(exchange.ExchangeId);

		Assert.NotNull(result);
		Assert.Equal(exchange.ExchangeId, result.ExchangeId);
		Assert.Equal(exchange.ChatId, result.ChatId);
	}

	[Fact]
	public async Task GetExchangeById_ExchangeDoesNotExist_ReturnsNull()
	{
		var result = await _service.GetExchangeById(9999);

		Assert.Null(result);
	}


	[Fact]
	public async Task GetExchangeWithMatch_ExchangeDoesNotExist_ReturnsNull()
	{
		var result = await _service.GetExchangeWithMatch(9999);

		Assert.Null(result);
	}

	[Fact]
	public async Task GetExchangeByChatIdWithMatch_ChatDoesNotExist_ReturnsNull()
	{
		var result = await _service.GetExchangeByChatIdWithMatch(Guid.NewGuid());

		Assert.Null(result);
	}

	[Fact]
	public async Task GetAllExchanges_NoExchanges_ReturnsEmptyList()
	{
		var result = await _service.GetAllExchanges();

		Assert.Empty(result);
	}

	// DELETE TESTS
	[Fact]
	public async Task DeleteExchange_ExchangeExists_DeletesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 78,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var result = await _service.DeleteExchange(exchange.ExchangeId);

		Assert.True(result);
		var deletedExchange = await _db.Exchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchange.ExchangeId);
		Assert.Null(deletedExchange);
	}

	[Fact]
	public async Task DeleteExchange_ExchangeDoesNotExist_ThrowsException()
	{
		var ex = await Assert.ThrowsAsync<Exception>(() => _service.DeleteExchange(9999));

		Assert.Contains("no encontrado", ex.Message.ToLower());
	}

	// VALIDATE UNIQUENESS TESTS
	[Fact]
	public async Task CreateExchange_ChatAndMatchAlreadyUsedTogether_ThrowsInvalidOperationException()
	{
		var chatId = Guid.NewGuid();
		var chat = new Chat { Id = chatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
		_db.Chats.Add(chat);

		var match = new Match
		{
			Id = 110,
			User1Id = Guid.NewGuid(),
			User2Id = Guid.NewGuid(),
			Book1Id = 1,
			Book2Id = 2,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};
		_db.Matches.Add(match);

		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = chatId,
			MatchId = 110,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.CreateExchange(chatId, 110));

		Assert.Contains("ya usado", ex.Message);
	}

	[Fact]
	public async Task CreateExchange_ChatAlreadyUsedInOtherExchange_ThrowsInvalidOperationException()
	{
		var chatId = Guid.NewGuid();
		var chat = new Chat { Id = chatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
		_db.Chats.Add(chat);

		_db.Matches.AddRange(
			new Match
			{
				Id = 120,
				User1Id = Guid.NewGuid(),
				User2Id = Guid.NewGuid(),
				Book1Id = 1,
				Book2Id = 2,
				Status = MatchStatus.NEW,
				CreatedAt = DateTime.UtcNow
			},
			new Match
			{
				Id = 121,
				User1Id = Guid.NewGuid(),
				User2Id = Guid.NewGuid(),
				Book1Id = 3,
				Book2Id = 4,
				Status = MatchStatus.NEW,
				CreatedAt = DateTime.UtcNow
			}
		);

		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = chatId,
			MatchId = 120,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.CreateExchange(chatId, 121));

		Assert.Contains("ya usado en otro exchange", ex.Message);
	}

	[Fact]
	public async Task CreateExchange_MatchAlreadyUsedInOtherExchange_ThrowsInvalidOperationException()
	{
		var usedChatId = Guid.NewGuid();
		var newChatId = Guid.NewGuid();
		_db.Chats.AddRange(
			new Chat { Id = usedChatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow },
			new Chat { Id = newChatId, Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow }
		);

		var match = new Match
		{
			Id = 130,
			User1Id = Guid.NewGuid(),
			User2Id = Guid.NewGuid(),
			Book1Id = 1,
			Book2Id = 2,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};
		_db.Matches.Add(match);

		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = usedChatId,
			MatchId = 130,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.CreateExchange(newChatId, 130));

		Assert.Contains("ya usado en otro exchange", ex.Message);
	}

	// STATUS TRANSITION TESTS
	[Fact]
	public async Task UpdateExchangeStatus_FromNegotiatingToRejected_UpdatesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 141,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var updated = await _service.UpdateExchangeStatus(exchange.ExchangeId, ExchangeStatus.REJECTED);

		Assert.Equal(ExchangeStatus.REJECTED, updated.Status);
	}

	[Fact]
	public async Task UpdateExchangeStatus_FromAcceptedByOneToAccepted_UpdatesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 151,
			Status = ExchangeStatus.ACCEPTED_BY_1,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var updated = await _service.UpdateExchangeStatus(exchange.ExchangeId, ExchangeStatus.ACCEPTED_BY_2);

		Assert.Equal(ExchangeStatus.ACCEPTED_BY_2, updated.Status);
	}

	[Fact]
	public async Task UpdateExchangeStatus_ToIncident_UpdatesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ChatId = Guid.NewGuid(),
			MatchId = 161,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);
		await _db.SaveChangesAsync();

		var updated = await _service.UpdateExchangeStatus(exchange.ExchangeId, ExchangeStatus.INCIDENT);

		Assert.Equal(ExchangeStatus.INCIDENT, updated.Status);
	}

	[Fact]
	public async Task UpdateExchangeStatus_ExchangeDoesNotExist_ThrowsInvalidOperationException()
	{
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.UpdateExchangeStatus(9999, ExchangeStatus.ACCEPTED));

		Assert.Contains("no encontrado", ex.Message);
	}
}
