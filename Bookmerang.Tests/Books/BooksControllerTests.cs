using System.Security.Claims;
using Bookmerang.Api.Controllers.Books;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Books;

public class BooksControllerTests
{
    [Fact]
    public async Task CreateDraft_UsuarioAutenticado_DevuelveCreated()
    {
        var service = new Mock<IBookService>();
        var controller = CreateController(service);

        var request = new CreateBookDraftRequest
        {
            GenreIds = new List<int> { 1 },
            LanguageIds = new List<int> { 1 }
        };

        var created = new BookDetailDTO { Id = 123 };
        service.Setup(s => s.CreateDraftAsync(It.IsAny<string>(), request, It.IsAny<CancellationToken>()))
               .ReturnsAsync(created);

        var result = await controller.CreateDraft(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task GetById_CuandoExiste_DevuelveOk()
    {
        var service = new Mock<IBookService>();
        var controller = CreateController(service);

        var dto = new BookDetailDTO { Id = 10 };
        service.Setup(s => s.GetByIdAsync(10, It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(dto);

        var result = await controller.GetById(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task GetMyLibrary_ConQuery_DevuelveOk()
    {
        var service = new Mock<IBookService>();
        var controller = CreateController(service);

        var query = new LibraryQuery();
        var paged = new PagedResult<BookListItemDTO>
        {
            Items = new List<BookListItemDTO>(),
            Total = 0,
            Page = 1,
            PageSize = 10
        };

        service.Setup(s => s.GetMyLibraryAsync(It.IsAny<string>(), query, It.IsAny<CancellationToken>()))
               .ReturnsAsync(paged);

        var result = await controller.GetMyLibrary(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task Delete_Exito_DevuelveNoContent()
    {
        var service = new Mock<IBookService>();
        var controller = CreateController(service);

        var result = await controller.Delete(55, CancellationToken.None);

        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContent.StatusCode);
        service.Verify(s => s.DeleteAsync(55, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BooksController CreateController(Mock<IBookService> service)
    {
        var controller = new BooksController(service.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "supabase-user-id"),
            new("sub", "supabase-user-id")
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }
}