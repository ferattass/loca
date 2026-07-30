using Loca.Application.Common.Models;
using Loca.Application.Features.Organizers.Common;
using Loca.Domain.Enums;
using Loca.Domain.Repositories;
using MediatR;

namespace Loca.Application.Features.Organizers.GetOrganizerApplications;

/// <remarks>
/// Admin kuyrugu. Durum verilmezse tum basvurular doner; varsayilan olarak
/// bekleyenleri istemek istemcinin karari.
/// </remarks>
public sealed record GetOrganizerApplicationsQuery(OrganizerApplicationStatus? Status)
    : IRequest<Result<IReadOnlyList<OrganizerApplicationItem>>>;

internal sealed class GetOrganizerApplicationsQueryHandler(IOrganizerRepository organizers)
    : IRequestHandler<GetOrganizerApplicationsQuery, Result<IReadOnlyList<OrganizerApplicationItem>>>
{
    public async Task<Result<IReadOnlyList<OrganizerApplicationItem>>> Handle(
        GetOrganizerApplicationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kayitlar = await organizers.GetApplicationsAsync(request.Status, cancellationToken);

        var liste = kayitlar
            .Select(application => new OrganizerApplicationItem(
                application.Id,
                application.UserId,
                application.User?.FullName ?? string.Empty,
                application.User?.Email ?? string.Empty,
                application.CompanyName,
                application.TaxNumber,
                application.ContactEmail,
                application.ContactPhone,
                application.Website,
                application.DocumentFileId,
                application.Status,
                application.CreatedAt,
                application.ReviewedAt,
                application.RejectionReason))
            .ToList();

        return Result.Success<IReadOnlyList<OrganizerApplicationItem>>(liste);
    }
}
