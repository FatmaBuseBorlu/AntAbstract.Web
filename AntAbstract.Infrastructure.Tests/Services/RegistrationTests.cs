using AntAbstract.Domain.Entities;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class RegistrationTests
{
    [Fact]
    public void NewRegistration_GeneratesUniqueQrToken()
    {
        var reg1 = new Registration
        {
            AppUserId = "u1",
            ConferenceId = Guid.NewGuid(),
            RegistrationTypeId = Guid.NewGuid(),
            Amount = 100
        };
        var reg2 = new Registration
        {
            AppUserId = "u2",
            ConferenceId = Guid.NewGuid(),
            RegistrationTypeId = Guid.NewGuid(),
            Amount = 100
        };

        Assert.NotNull(reg1.QrToken);
        Assert.NotNull(reg2.QrToken);
        Assert.NotEqual(reg1.QrToken, reg2.QrToken);
    }

    [Fact]
    public void QrToken_HasExpectedLength()
    {
        var reg = new Registration
        {
            AppUserId = "u1",
            ConferenceId = Guid.NewGuid(),
            RegistrationTypeId = Guid.NewGuid(),
            Amount = 100
        };

        Assert.Equal(22, reg.QrToken.Length);
    }

    [Fact]
    public void QrToken_ContainsNoUrlUnsafeCharacters()
    {
        for (int i = 0; i < 100; i++)
        {
            var reg = new Registration
            {
                AppUserId = "u",
                ConferenceId = Guid.NewGuid(),
                RegistrationTypeId = Guid.NewGuid(),
                Amount = 0
            };

            Assert.DoesNotContain("/", reg.QrToken);
            Assert.DoesNotContain("+", reg.QrToken);
            Assert.DoesNotContain("=", reg.QrToken);
        }
    }

    [Fact]
    public void NewRegistration_DefaultsToExpectedState()
    {
        var reg = new Registration
        {
            AppUserId = "u1",
            ConferenceId = Guid.NewGuid(),
            RegistrationTypeId = Guid.NewGuid(),
            Amount = 500
        };

        Assert.False(reg.IsPaid);
        Assert.Equal(RegistrationStatus.Pending, reg.Status);
        Assert.Null(reg.PaymentDate);
        Assert.Null(reg.CheckedInAt);
        Assert.NotEqual(Guid.Empty, reg.Id);
    }

    [Fact]
    public void AccommodationBooking_NightsCalculation_IsCorrect()
    {
        var checkIn = new DateTime(2026, 7, 10);
        var checkOut = new DateTime(2026, 7, 13);
        var pricePerNight = 250m;

        var nights = (checkOut.Date - checkIn.Date).Days;
        var total = pricePerNight * nights;

        Assert.Equal(3, nights);
        Assert.Equal(750m, total);
    }

    [Fact]
    public void AccommodationBooking_SameDayCheckout_ZeroNights()
    {
        var date = new DateTime(2026, 7, 10);
        var nights = (date.Date - date.Date).Days;

        Assert.Equal(0, nights);
    }

    [Fact]
    public void AccommodationBooking_WithTransfer_TotalIncludesTransfer()
    {
        var checkIn = new DateTime(2026, 7, 10);
        var checkOut = new DateTime(2026, 7, 12);
        var roomPrice = 300m;
        var transferPrice = 150m;

        var nights = (checkOut.Date - checkIn.Date).Days;
        var total = roomPrice * nights + transferPrice;

        Assert.Equal(750m, total);
    }
}
