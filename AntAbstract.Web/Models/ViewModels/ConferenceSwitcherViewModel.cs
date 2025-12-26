using System;
using System.Collections.Generic;

namespace AntAbstract.Web.Models.ViewModels
{
    public record ConferenceSwitcherModel(
        Guid? SelectedConferenceId,
        string? CurrentConferenceName,
        string ReturnUrl,
        List<ConferenceSwitcherItemModel> Conferences);

    public record ConferenceSwitcherItemModel(
        Guid Id,
        string Title,
        string TenantSlug);
}