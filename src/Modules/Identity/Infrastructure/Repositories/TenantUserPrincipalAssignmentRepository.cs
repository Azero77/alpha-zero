using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using MassTransit.Serialization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class TenantUserPrincipalAssignmentRepository : BaseRepository<AppDbContext, TenantUserPrincipalAssignment>, ITenantUserPrincipalAssignmentRepository
{
    private readonly IPrincipalRepository _principalRepository;
    public TenantUserPrincipalAssignmentRepository(AppDbContext context, IPrincipalRepository principalRepository) : base(context)
    {
        _principalRepository = principalRepository;
    }

    public async Task<TenantUserPrincipalAssignment?> GetActiveAssignment(Guid tenantUserId, string? resourceArn = null, CancellationToken token = default)
    {
        //the resource will be az:{serviceType}:{tenantId}:{resourcePath}
        // we will see if any record in the database matched to check if the requested resourceArn is contained inside the Record
        //lets say there is a course with az:course:TenantA:course/math101
        //and the resource that needs a permission to pass is a video inside it like az:video:TenantA:course/math101/section/SectionA/Item/ItemB/videoA
        //because course/math101 is contained inside the requested arn it will evaluate to true because the video is contained inside the course
        //if multiple assignments to the same user for the requested resource was found, we will get the latest one for it
        if(resourceArn is null)
            return await _context.TenantPrincipalAssignments.FirstOrDefaultAsync(s => s.TenantUser.Id == tenantUserId, token);

        var path = ResourceArn.Create(resourceArn).Value.ResourcePath;
        var assignmentsForUser = await _context.TenantPrincipalAssignments
            .AsNoTracking()
            .Where(t => t.TenantUser.Id == tenantUserId)
            .ToListAsync(token);

        
        var assignment =  assignmentsForUser
           .Where(a => a.Resource.IsRequestedPathContainedInResource(path))
           .OrderByDescending(a => a.TimeCreated)
           .FirstOrDefault();

        if (assignment is null) return null;
        // we need to load the principal and policy for the assignment because they are not loaded in the query above

        var principal = await _principalRepository.GetById(assignment.PrincipalId, token);
        assignment.AttachPrincipal(principal!);

        return assignment;

    }


}
