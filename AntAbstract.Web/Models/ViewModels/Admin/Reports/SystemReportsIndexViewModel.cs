using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels.Admin.Reports
{
    public class SystemReportsIndexViewModel
    {
        public int TotalInstitutions { get; set; }

        public int TotalConferences { get; set; }

        public int TotalUsers { get; set; }

        public int TotalAdmins { get; set; }

        public int TotalAuthors { get; set; }

        public int TotalReviewers { get; set; }

        public int TotalSubmissions { get; set; }

        public int TotalRegistrations { get; set; }

        public int PaidRegistrations { get; set; }

        public int PendingPayments { get; set; }

        public decimal TotalRevenue { get; set; }

        public List<RecentUserReportItem> RecentUsers { get; set; } = new List<RecentUserReportItem>();

        public List<RecentConferenceReportItem> RecentConferences { get; set; } = new List<RecentConferenceReportItem>();

        public List<ConferenceSubmissionReportItem> ConferenceSubmissionReports { get; set; } = new List<ConferenceSubmissionReportItem>();

        public List<TenantConferenceReportItem> TenantConferenceReports { get; set; } = new List<TenantConferenceReportItem>();
    }

    public class RecentUserReportItem
    {
        public string Id { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string TenantName { get; set; } = string.Empty;

        public DateTime? CreatedDate { get; set; }
    }

    public class RecentConferenceReportItem
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string TenantName { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }

    public class ConferenceSubmissionReportItem
    {
        public Guid ConferenceId { get; set; }

        public string ConferenceTitle { get; set; } = string.Empty;

        public string TenantName { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public int SubmissionCount { get; set; }
    }

    public class TenantConferenceReportItem
    {
        public Guid TenantId { get; set; }

        public string TenantName { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public int ConferenceCount { get; set; }

        public int UserCount { get; set; }
    }
}