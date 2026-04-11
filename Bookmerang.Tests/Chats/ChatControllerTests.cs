using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Controllers.Chats;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Exceptions;
using Xunit;

namespace Bookmerang.Tests.Chats;

public class ChatControllerTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly AppDbContext _db;
    private readonly ChatController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly string _supabaseId = "test-supabase-id";

    public ChatControllerTests()
    {
        _mockChatService = new Mock<IChatService>();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _controller = new ChatController(_mockChatService.Object, _db);
        
        SetupUserInDb();
        SetupControllerContext();
    }

    private void SetupUserInDb()
    {
        var user = new BaseUser
        {
            Id = _currentUserId,
            SupabaseId = _supabaseId,
            Email = "test@test.com",
            Username = "testuser",
            Name = "Test User",
            Location = new Point(0, 0) { SRID = 4326 }
        };
        _db.Users.Add(user);
        _db.SaveChanges();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", _supabaseId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetMyChats_ShouldReturnOk_WithChats()
    {
        var chats = new List<ChatDto> { new ChatDto(Guid.NewGuid(), "INDIVIDUAL", DateTime.UtcNow, new List<ChatParticipantDto>(), null) };
        _mockChatService.Setup(s => s.GetUserChats(_currentUserId)).ReturnsAsync(chats);

        var result = await _controller.GetMyChats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(chats, okResult.Value);
    }

    [Fact]
    public async Task GetChat_ShouldReturnOk_WhenChatExists()
    {
        var chatId = Guid.NewGuid();
        var chat = new ChatDto(chatId, "INDIVIDUAL", DateTime.UtcNow, new List<ChatParticipantDto>(), null);
        _mockChatService.Setup(s => s.GetChatById(chatId, _currentUserId)).ReturnsAsync(chat);

        var result = await _controller.GetChat(chatId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(chat, okResult.Value);
    }

    [Fact]
    public async Task GetChat_ShouldThrowNotFound_WhenChatDoesNotExist()
    {
        var chatId = Guid.NewGuid();
        _mockChatService.Setup(s => s.GetChatById(chatId, _currentUserId))
            .ThrowsAsync(new NotFoundException("not found"));

        await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetChat(chatId));
    }

    [Fact]
    public async Task GetMessages_ShouldReturnOk_WhenUserIsParticipant()
    {
        var chatId = Guid.NewGuid();
        var messages = new List<MessageDto>();
        _mockChatService.Setup(s => s.IsParticipant(chatId, _currentUserId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.GetMessages(chatId, _currentUserId, 1, 50)).ReturnsAsync(messages);

        var result = await _controller.GetMessages(chatId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(messages, okResult.Value);
    }

    [Fact]
    public async Task GetMessages_ShouldReturnForbid_WhenUserIsNotParticipant()
    {
        var chatId = Guid.NewGuid();
        _mockChatService.Setup(s => s.IsParticipant(chatId, _currentUserId)).ReturnsAsync(false);

        var result = await _controller.GetMessages(chatId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnCreated_WhenSuccessful()
    {
        var chatId = Guid.NewGuid();
        var request = new SendMessageRequest("Hello");
        var messageDto = new MessageDto(1, chatId, _currentUserId, "testuser", "Hello", DateTime.UtcNow);
        _mockChatService.Setup(s => s.SendMessage(chatId, _currentUserId, request.Body)).ReturnsAsync(messageDto);

        var result = await _controller.SendMessage(chatId, request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(messageDto, createdResult.Value);
    }

    [Fact]
    public async Task CreateChat_ShouldReturnCreated_WhenSuccessful()
    {
        var participantIds = new List<Guid> { _currentUserId, Guid.NewGuid() };
        var request = new CreateChatRequest(ChatType.EXCHANGE, participantIds);
        var chatDto = new ChatDto(Guid.NewGuid(), "INDIVIDUAL", DateTime.UtcNow, new List<ChatParticipantDto>(), null);
        
        _mockChatService.Setup(s => s.CreateChat(request.Type, It.IsAny<List<Guid>>())).ReturnsAsync(chatDto);

        var result = await _controller.CreateChat(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(chatDto, createdResult.Value);
    }

    [Fact]
    public async Task StartTyping_ShouldReturnNoContent_WhenSuccessful()
    {
        var chatId = Guid.NewGuid();
        _mockChatService.Setup(s => s.StartTyping(chatId, _currentUserId)).ReturnsAsync(true);

        var result = await _controller.StartTyping(chatId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task StopTyping_ShouldReturnNoContent()
    {
        var chatId = Guid.NewGuid();
        _mockChatService.Setup(s => s.StopTyping(chatId, _currentUserId)).ReturnsAsync(true);

        var result = await _controller.StopTyping(chatId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetTypingUsers_ShouldReturnOk()
    {
        var chatId = Guid.NewGuid();
        var typingUsers = new List<TypingUserDto>();
        _mockChatService.Setup(s => s.GetTypingUsers(chatId, _currentUserId)).ReturnsAsync(typingUsers);

        var result = await _controller.GetTypingUsers(chatId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(typingUsers, okResult.Value);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnBadRequest_WhenBodyIsEmpty()
    {
        var chatId = Guid.NewGuid();
        var request = new SendMessageRequest("");

        var result = await _controller.SendMessage(chatId, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateChat_ShouldReturnBadRequest_WhenFewerThanTwoParticipants()
    {
        var participantIds = new List<Guid> { _currentUserId };
        var request = new CreateChatRequest(ChatType.EXCHANGE, participantIds);

        var result = await _controller.CreateChat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
