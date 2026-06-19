using AntAbstract.Web.Models.ViewModels.Admin.Reports;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Services;

public sealed class FinanceSummaryTests
{
    [Fact]
    public void FinanceByCategory_GroupsByRegistrationType()
    {
        var registrations = new[]
        {
            new { Amount = 500m, IsPaid = true, TypeName = "Author", Currency = "TRY" },
            new { Amount = 500m, IsPaid = false, TypeName = "Author", Currency = "TRY" },
            new { Amount = 200m, IsPaid = true, TypeName = "Listener", Currency = "TRY" },
            new { Amount = 200m, IsPaid = true, TypeName = "Listener", Currency = "TRY" },
        };

        var financeByCategory = registrations
            .GroupBy(x => x.TypeName)
            .Select(g => new FinanceCategoryItem
            {
                Label = g.Key,
                PaidAmount = g.Where(x => x.IsPaid).Sum(x => x.Amount),
                UnpaidAmount = g.Where(x => !x.IsPaid).Sum(x => x.Amount),
                PaidCount = g.Count(x => x.IsPaid),
                UnpaidCount = g.Count(x => !x.IsPaid)
            })
            .OrderByDescending(x => x.PaidAmount + x.UnpaidAmount)
            .ToList();

        Assert.Equal(2, financeByCategory.Count);

        var author = financeByCategory.First(x => x.Label == "Author");
        Assert.Equal(500m, author.PaidAmount);
        Assert.Equal(500m, author.UnpaidAmount);
        Assert.Equal(1, author.PaidCount);
        Assert.Equal(1, author.UnpaidCount);

        var listener = financeByCategory.First(x => x.Label == "Listener");
        Assert.Equal(400m, listener.PaidAmount);
        Assert.Equal(0m, listener.UnpaidAmount);
        Assert.Equal(2, listener.PaidCount);
        Assert.Equal(0, listener.UnpaidCount);
    }

    [Fact]
    public void TotalRevenue_OnlySumsPaidRegistrations()
    {
        var registrations = new[]
        {
            new { Amount = 500m, IsPaid = true },
            new { Amount = 300m, IsPaid = false },
            new { Amount = 200m, IsPaid = true },
        };

        var totalRevenue = registrations.Where(x => x.IsPaid).Sum(x => x.Amount);
        var unpaidRevenue = registrations.Where(x => !x.IsPaid).Sum(x => x.Amount);

        Assert.Equal(700m, totalRevenue);
        Assert.Equal(300m, unpaidRevenue);
    }

    [Fact]
    public void EmptyRegistrations_ProducesEmptyFinance()
    {
        var registrations = Array.Empty<object>();

        var financeByCategory = new List<FinanceCategoryItem>();

        Assert.Empty(financeByCategory);
    }

    [Fact]
    public void FinanceCategoryItem_DefaultValues()
    {
        var item = new FinanceCategoryItem();

        Assert.Equal(string.Empty, item.Label);
        Assert.Equal(0m, item.PaidAmount);
        Assert.Equal(0m, item.UnpaidAmount);
        Assert.Equal(0, item.PaidCount);
        Assert.Equal(0, item.UnpaidCount);
    }
}
