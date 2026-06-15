using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AntAbstract.Infrastructure.Tests.Context;

public class AppDbContextTenantIsolationTests
{
    [Fact]
    public async Task QueryFilter_UsesCurrentTenant_WhenModelWasBuiltWithoutTenant()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");

        await using (var seedContext = new AppDbContext(options, new TenantContext()))
        {
            seedContext.Submissions.AddRange(
                CreateSubmission(tenantA.Id, "Tenant A submission"),
                CreateSubmission(tenantB.Id, "Tenant B submission"));

            await seedContext.SaveChangesAsync();
        }

        await using var tenantAContext = new AppDbContext(
            options,
            new TenantContext { Current = tenantA });
        await using var tenantBContext = new AppDbContext(
            options,
            new TenantContext { Current = tenantB });

        var tenantATitles = await tenantAContext.Submissions
            .Select(x => x.Title)
            .ToListAsync();
        var tenantBTitles = await tenantBContext.Submissions
            .Select(x => x.Title)
            .ToListAsync();

        Assert.Equal(["Tenant A submission"], tenantATitles);
        Assert.Equal(["Tenant B submission"], tenantBTitles);
    }

    [Fact]
    public void Model_ContainsQueryFilters_ForAllConferenceBoundEntities()
    {
        using var context = new AppDbContext(CreateOptions(), new TenantContext());
        var expectedEntityTypes = new[]
        {
            typeof(Conference),
            typeof(ConferenceTopic),
            typeof(Session),
            typeof(Registration),
            typeof(RegistrationType),
            typeof(Payment),
            typeof(Certificate),
            typeof(ConferenceAttendance),
            typeof(Hotel),
            typeof(RoomType),
            typeof(TransferOption),
            typeof(AccommodationBooking),
            typeof(Submission),
            typeof(ReviewAssignment),
            typeof(Review),
            typeof(SubmissionAuthor),
            typeof(SubmissionFile),
            typeof(ConferencePageBlock)
        };

        foreach (var entityType in expectedEntityTypes)
        {
            var queryFilter = context.Model
                .FindEntityType(entityType)?
                .GetQueryFilter();

            Assert.True(
                queryFilter != null,
                $"A tenant query filter is required for {entityType.Name}.");
        }
    }

    [Fact]
    public async Task RelatedQueryFilters_IsolateConferenceHotelAndSubmissionData()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var conferenceA = CreateConference(tenantA.Id, "Conference A");
        var conferenceB = CreateConference(tenantB.Id, "Conference B");
        var registrationTypeA = CreateRegistrationType(conferenceA, "Type A");
        var registrationTypeB = CreateRegistrationType(conferenceB, "Type B");
        var hotelA = CreateHotel(conferenceA, "Hotel A");
        var hotelB = CreateHotel(conferenceB, "Hotel B");
        var submissionA = CreateSubmission(tenantA.Id, "Submission A", conferenceA);
        var submissionB = CreateSubmission(tenantB.Id, "Submission B", conferenceB);

        await using (var seedContext = new AppDbContext(options, new TenantContext()))
        {
            seedContext.AddRange(
                conferenceA,
                conferenceB,
                registrationTypeA,
                registrationTypeB,
                CreateRegistration(conferenceA, registrationTypeA, "user-a"),
                CreateRegistration(conferenceB, registrationTypeB, "user-b"),
                CreatePayment(conferenceA, "user-a"),
                CreatePayment(conferenceB, "user-b"),
                hotelA,
                hotelB,
                CreateRoomType(hotelA, "Room A"),
                CreateRoomType(hotelB, "Room B"),
                submissionA,
                submissionB,
                CreateReviewAssignment(submissionA, "reviewer-a"),
                CreateReviewAssignment(submissionB, "reviewer-b"),
                CreateSubmissionAuthor(submissionA, "Author A"),
                CreateSubmissionAuthor(submissionB, "Author B"),
                CreateSubmissionFile(submissionA, "a.pdf"),
                CreateSubmissionFile(submissionB, "b.pdf"));

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(
            options,
            new TenantContext { Current = tenantA });

        Assert.Equal(["Conference A"], await context.Conferences.Select(x => x.Title).ToListAsync());
        Assert.Equal(["user-a"], await context.Registrations.Select(x => x.AppUserId).ToListAsync());
        Assert.Equal(["user-a"], await context.Payments.Select(x => x.AppUserId).ToListAsync());
        Assert.Equal(["Room A"], await context.RoomTypes.Select(x => x.Name).ToListAsync());
        Assert.Equal(["reviewer-a"], await context.ReviewAssignments.Select(x => x.ReviewerId).ToListAsync());
        Assert.Equal(["Author A"], await context.SubmissionAuthors.Select(x => x.FirstName).ToListAsync());
        Assert.Equal(["a.pdf"], await context.SubmissionFiles.Select(x => x.FileName).ToListAsync());
    }

    [Fact]
    public async Task QueryFilters_AllowGlobalQueries_WhenNoTenantIsResolved()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");

        await using (var seedContext = new AppDbContext(options, new TenantContext()))
        {
            seedContext.Conferences.AddRange(
                CreateConference(tenantA.Id, "Conference A"),
                CreateConference(tenantB.Id, "Conference B"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options, new TenantContext());

        Assert.Equal(2, await context.Conferences.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_AssignsCurrentTenant_WhenTenantIdIsEmpty()
    {
        var tenant = CreateTenant("tenant-a");
        var context = new AppDbContext(
            CreateOptions(),
            new TenantContext { Current = tenant });
        var submission = CreateSubmission(Guid.Empty, "New submission");

        context.Submissions.Add(submission);
        await context.SaveChangesAsync();

        Assert.Equal(tenant.Id, submission.TenantId);
    }

    [Fact]
    public async Task SaveChanges_RejectsEntityOwnedByAnotherTenant()
    {
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var context = new AppDbContext(
            CreateOptions(),
            new TenantContext { Current = tenantA });

        context.Submissions.Add(
            CreateSubmission(tenantB.Id, "Other tenant submission"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        Assert.Contains("another tenant", exception.Message);
    }

    [Fact]
    public async Task SaveChanges_RejectsTenantIdChange()
    {
        var options = CreateOptions();
        var tenantA = CreateTenant("tenant-a");
        var tenantB = CreateTenant("tenant-b");
        var submission = CreateSubmission(tenantA.Id, "Tenant A submission");

        await using (var seedContext = new AppDbContext(options, new TenantContext()))
        {
            seedContext.Submissions.Add(submission);
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(
            options,
            new TenantContext { Current = tenantA });
        var existingSubmission = await context.Submissions.SingleAsync();

        existingSubmission.TenantId = tenantB.Id;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        Assert.Contains("cannot be changed", exception.Message);
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant-isolation-{Guid.NewGuid()}")
            .Options;
    }

    private static Tenant CreateTenant(string slug)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug,
            Slug = slug
        };
    }

    private static Conference CreateConference(Guid tenantId, string title)
    {
        return new Conference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        };
    }

    private static Submission CreateSubmission(
        Guid tenantId,
        string title,
        Conference? conference = null)
    {
        return new Submission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Abstract = "Abstract",
            Keywords = "tenant",
            AuthorId = "author",
            ConferenceId = conference?.Id ?? Guid.NewGuid(),
            Conference = conference!
        };
    }

    private static RegistrationType CreateRegistrationType(
        Conference conference,
        string name)
    {
        return new RegistrationType
        {
            Id = Guid.NewGuid(),
            ConferenceId = conference.Id,
            Conference = conference,
            Name = name,
            Price = 100
        };
    }

    private static Registration CreateRegistration(
        Conference conference,
        RegistrationType registrationType,
        string userId)
    {
        return new Registration
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            ConferenceId = conference.Id,
            Conference = conference,
            RegistrationTypeId = registrationType.Id,
            RegistrationType = registrationType,
            Amount = registrationType.Price
        };
    }

    private static Payment CreatePayment(Conference conference, string userId)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            ConferenceId = conference.Id,
            Conference = conference,
            Amount = 100
        };
    }

    private static Hotel CreateHotel(Conference conference, string name)
    {
        return new Hotel
        {
            Id = Guid.NewGuid(),
            ConferenceId = conference.Id,
            Conference = conference,
            Name = name,
            RoomTypes = new List<RoomType>()
        };
    }

    private static RoomType CreateRoomType(Hotel hotel, string name)
    {
        return new RoomType
        {
            Id = Guid.NewGuid(),
            HotelId = hotel.Id,
            Hotel = hotel,
            Name = name
        };
    }

    private static ReviewAssignment CreateReviewAssignment(
        Submission submission,
        string reviewerId)
    {
        return new ReviewAssignment
        {
            SubmissionId = submission.Id,
            Submission = submission,
            ReviewerId = reviewerId
        };
    }

    private static SubmissionAuthor CreateSubmissionAuthor(
        Submission submission,
        string firstName)
    {
        return new SubmissionAuthor
        {
            SubmissionId = submission.Id,
            Submission = submission,
            FirstName = firstName,
            LastName = "Author"
        };
    }

    private static SubmissionFile CreateSubmissionFile(
        Submission submission,
        string fileName)
    {
        return new SubmissionFile
        {
            SubmissionId = submission.Id,
            Submission = submission,
            FileName = fileName,
            StoredFileName = fileName,
            FilePath = $"/files/{fileName}",
            UploadedAt = DateTime.UtcNow
        };
    }
}
