using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bookmeran.Controllers;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Auth;

public class AuthControllerBookdropPaymentTests
{
    private static IConfiguration BuildConfig(bool paymentRequired)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bookdrop:RequirePayment"] = paymentRequired ? "true" : "false"
            })
            .Build();
    }

    private static RegisterBusinessRequest BuildBusinessRequest(string? paymentSessionId = null)
    {
        return new RegisterBusinessRequest
        {
            Email = "bookdrop@test.com",
            Password = "123456",
            Username = "bookdrop_user",
            Name = "Bookdrop User",
            ProfilePhoto = "",
            NombreEstablecimiento = "Bookdrop Place",
            AddressText = "Calle Falsa 123",
            Latitud = 40.4168,
            Longitud = -3.7038,
            PaymentSessionId = paymentSessionId
        };
    }

    private static BaseUser BuildBookdropUser()
    {
        return new BaseUser
        {
            Id = Guid.NewGuid(),
            SupabaseId = "sup-bookdrop",
            Email = "bookdrop@test.com",
            Username = "bookdrop_user",
            Name = "Bookdrop User",
            ProfilePhoto = "",
            UserType = BaseUserType.BOOKDROP_USER,
            Location = new Point(-3.7038, 40.4168) { SRID = 4326 }
        };
    }

    [Fact]
    public async Task RegisterBusiness_ShouldReturnBadRequest_WhenPaymentSessionIdMissing_AndPaymentRequired()
    {
        var authMock = new Mock<IAuthService>();
        var stripeMock = new Mock<IStripeSubscriptionService>();
        var controller = new AuthController(authMock.Object, stripeMock.Object, BuildConfig(paymentRequired: true));

        var result = await controller.RegisterBusiness(BuildBusinessRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Para crear un BookDrop debes completar primero el pago de 1 EUR.", badRequest.Value);

        stripeMock.Verify(
            s => s.ValidateBookdropRegistrationPaymentAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        authMock.Verify(
            s => s.RegisterBusiness(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Point>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterBusiness_ShouldReturnBadRequest_WhenPaymentValidationFails()
    {
        var authMock = new Mock<IAuthService>();
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock
            .Setup(s => s.ValidateBookdropRegistrationPaymentAsync("cs_invalid", "bookdrop@test.com"))
            .ReturnsAsync(false);
        var controller = new AuthController(authMock.Object, stripeMock.Object, BuildConfig(paymentRequired: true));

        var result = await controller.RegisterBusiness(BuildBusinessRequest("cs_invalid"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No se ha validado un pago de 1 EUR para este usuario.", badRequest.Value);

        stripeMock.Verify(
            s => s.ValidateBookdropRegistrationPaymentAsync("cs_invalid", "bookdrop@test.com"),
            Times.Once);
    }

    [Fact]
    public async Task RegisterBusiness_ShouldCreateUser_WhenPaymentValidationSucceeds()
    {
        var authMock = new Mock<IAuthService>();
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock
            .Setup(s => s.ValidateBookdropRegistrationPaymentAsync("cs_ok", "bookdrop@test.com"))
            .ReturnsAsync(true);

        var createdUser = BuildBookdropUser();
        authMock
            .Setup(s => s.RegisterBusiness(
                "bookdrop@test.com",
                "123456",
                "bookdrop_user",
                "Bookdrop User",
                "",
                It.IsAny<Point>(),
                "Bookdrop Place",
                "Calle Falsa 123"))
            .ReturnsAsync((createdUser, false, (string?)null));
        authMock
            .Setup(s => s.Login("bookdrop@test.com", "123456"))
            .ReturnsAsync((createdUser, "token-bookdrop", (string?)null));

        var controller = new AuthController(authMock.Object, stripeMock.Object, BuildConfig(paymentRequired: true));
        var result = await controller.RegisterBusiness(BuildBusinessRequest("cs_ok"));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AuthController.GetPerfil), created.ActionName);
        var response = Assert.IsType<AuthResponse>(created.Value);
        Assert.Equal("token-bookdrop", response.AccessToken);

        stripeMock.Verify(
            s => s.ValidateBookdropRegistrationPaymentAsync("cs_ok", "bookdrop@test.com"),
            Times.Once);
        authMock.Verify(
            s => s.RegisterBusiness(
                "bookdrop@test.com",
                "123456",
                "bookdrop_user",
                "Bookdrop User",
                "",
                It.IsAny<Point>(),
                "Bookdrop Place",
                "Calle Falsa 123"),
            Times.Once);
    }

    [Fact]
    public async Task RegisterBusiness_ShouldSkipPayment_WhenDisabledInConfig()
    {
        var authMock = new Mock<IAuthService>();
        var createdUser = BuildBookdropUser();
        authMock
            .Setup(s => s.RegisterBusiness(
                "bookdrop@test.com",
                "123456",
                "bookdrop_user",
                "Bookdrop User",
                "",
                It.IsAny<Point>(),
                "Bookdrop Place",
                "Calle Falsa 123"))
            .ReturnsAsync((createdUser, false, (string?)null));
        authMock
            .Setup(s => s.Login("bookdrop@test.com", "123456"))
            .ReturnsAsync((createdUser, "token-bookdrop", (string?)null));

        var controller = new AuthController(authMock.Object, null, BuildConfig(paymentRequired: false));
        var result = await controller.RegisterBusiness(BuildBusinessRequest());

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<AuthResponse>(created.Value);
        Assert.Equal("token-bookdrop", response.AccessToken);
    }

    [Fact]
    public async Task CreateBusinessCheckout_ShouldReturnCheckoutUrl_WhenStripeConfigured()
    {
        var authMock = new Mock<IAuthService>();
        var stripeMock = new Mock<IStripeSubscriptionService>();
        stripeMock
            .Setup(s => s.CreateBookdropRegistrationCheckoutSessionAsync("bookdrop@test.com"))
            .ReturnsAsync("https://checkout.stripe.com/test");

        var controller = new AuthController(authMock.Object, stripeMock.Object);
        var request = new CreateBookdropCheckoutRequest { Email = "bookdrop@test.com" };
        var result = await controller.CreateBusinessCheckout(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var checkoutUrl = ok.Value!.GetType().GetProperty("checkoutUrl")?.GetValue(ok.Value) as string;
        Assert.Equal("https://checkout.stripe.com/test", checkoutUrl);
    }

    [Fact]
    public async Task CreateBusinessCheckout_ShouldReturn500_WhenStripeMissing()
    {
        var authMock = new Mock<IAuthService>();
        var controller = new AuthController(authMock.Object, stripeSubscriptionService: null);
        var request = new CreateBookdropCheckoutRequest { Email = "bookdrop@test.com" };

        var result = await controller.CreateBusinessCheckout(request);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }
}

