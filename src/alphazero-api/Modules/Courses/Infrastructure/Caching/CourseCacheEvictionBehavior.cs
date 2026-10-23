using AlphaZero.Modules.Courses.Application.Courses.Commands.AddSection;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Create;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Delete;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;
using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Update;
using AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;
using AlphaZero.Modules.Courses.Application.Subjects.Commands.Create;
using AlphaZero.Modules.Courses.Application.Subjects.Commands.Delete;
using AlphaZero.Modules.Courses.Application.Subjects.Commands.Update;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Caching;

/// <summary>
/// Tag-based cache eviction for the Courses module's cached read surfaces.
/// Registered as the OUTERMOST pipeline behavior (first <c>AddOpenBehavior</c>) so its
/// eviction runs strictly AFTER the UnitOfWork commit — a concurrent cold read in the
/// eviction window can then only re-fill from committed data (no stale-refill race).
///
/// The mapping below is the single source of truth for "which command invalidates which
/// surface" (issue #33, user story 8): cached surface ▪ TTL ▪ eviction triggers are documented
/// in <see cref="CourseCacheKeys"/> and <c>Modules/Courses/CACHING.md</c>.
///
/// Command families intentionally NOT wired (their reads are uncached by design, invalidating
/// them would be dead code): enrollment commands (Enroll/CompleteItem/Deactivate/Enroll-from-saga),
/// playback ticket issuance, asset creation (pool surface uncached), GetSubjectById reads.
///
/// A redis/L2 eviction failure must never fail an already-committed command: log and continue.
/// </summary>
public class CourseCacheEvictionBehavior<TRequest, TResponse>(
    HybridCache cache,
    ITenantProvider tenantProvider,
    ILogger<CourseCacheEvictionBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, ErrorOr<TResponse>>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    public async Task<ErrorOr<TResponse>> Handle(TRequest request, RequestHandlerDelegate<ErrorOr<TResponse>> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken);

        if (request is not ICommand<TResponse> || result.IsError)
        {
            return result;
        }

        try
        {
            switch (request)
            {
                // ── Catalog family ──
                case CreateCourseCommand:
                    await cache.RemoveByTagAsync(CourseCacheKeys.CatalogTag(tenantProvider.GetTenant()), cancellationToken);
                    break;

                // ── Course identity mutations (#29 CRUD): title/description/subject and soft
                // delete change both the catalog row and the per-course family ──
                case UpdateCourseCommand updateCourse:
                    await EvictCourseAndCatalogAsync(updateCourse.CourseId, cancellationToken);
                    break;
                case DeleteCourseCommand deleteCourse:
                    await EvictCourseAndCatalogAsync(deleteCourse.CourseId, cancellationToken);
                    break;

                // ── Per-course family (tree view, overview, readiness) ──
                // Section/item mutations shape the curriculum tree and readiness.
                case AddSectionCommand addSection:
                    await EvictCourseAsync(addSection.CourseId, cancellationToken);
                    break;
                case RenameCourseSectionCommand renameSection:
                    await EvictCourseAsync(renameSection.CourseId, cancellationToken);
                    break;
                case DeleteCourseSectionCommand deleteSection:
                    await EvictCourseAsync(deleteSection.CourseId, cancellationToken);
                    break;
                case CreateCourseSectionItemCommand itemCreate:
                    await EvictCourseAsync(itemCreate.CourseId, cancellationToken);
                    break;
                case UpdateCourseSectionItemCommand itemUpdate:
                    await EvictCourseAsync(itemUpdate.CourseId, cancellationToken);
                    break;
                case DeleteCourseSectionItemCommand itemDelete:
                    await EvictCourseAsync(itemDelete.CourseId, cancellationToken);
                    break;
                case ReorderSectionsCommand reorderSections:
                    await EvictCourseAsync(reorderSections.CourseId, cancellationToken);
                    break;
                case ReorderItemsCommand reorderItems:
                    await EvictCourseAsync(reorderItems.CourseId, cancellationToken);
                    break;
                case UpsertCourseOverviewCommand overview:
                    await EvictCourseAsync(overview.CourseId, cancellationToken);
                    break;

                // Plans change only readiness, but share the per-course family.
                case AddPlanCommand addPlan:
                    await EvictCourseAsync(addPlan.CourseId, cancellationToken);
                    break;
                case UpdatePlanCommand updatePlan:
                    await EvictCourseAsync(updatePlan.CourseId, cancellationToken);
                    break;
                case RemovePlanCommand removePlan:
                    await EvictCourseAsync(removePlan.CourseId, cancellationToken);
                    break;

                // Asset assign/unassign/dismiss change the attached pool → tree + readiness.
                case AssignAssetToCurriculumCommand assign:
                    await EvictCourseAsync(assign.CourseId, cancellationToken);
                    break;
                case UnassignAssetFromCurriculumCommand unassign:
                    await EvictCourseAsync(unassign.CourseId, cancellationToken);
                    break;
                case DismissAssetCommand dismiss:
                    await EvictCourseAsync(dismiss.CourseId, cancellationToken);
                    break;

                // ── State transitions: per-course family + catalog (Status is part of both) ──
                case SubmitCourseForReviewCommand submit:
                    await EvictCourseAndCatalogAsync(submit.CourseId, cancellationToken);
                    break;
                case ApproveCourseCommand approve:
                    await EvictCourseAndCatalogAsync(approve.CourseId, cancellationToken);
                    break;
                case RejectCourseCommand reject:
                    await EvictCourseAndCatalogAsync(reject.CourseId, cancellationToken);
                    break;
                case PublishCourseCommand publish:
                    await EvictCourseAndCatalogAsync(publish.CourseId, cancellationToken);
                    break;
                case ArchiveCourseCommand archive:
                    await EvictCourseAndCatalogAsync(archive.CourseId, cancellationToken);
                    break;

                // ── Subjects family ──
                case CreateSubjectCommand:
                    await EvictSubjectsAsync(cancellationToken);
                    break;
                // A renamed/deleted subject also changes SubjectName in every catalog row.
                case UpdateSubjectCommand or DeleteSubjectCommand:
                    await EvictSubjectsAsync(cancellationToken);
                    await cache.RemoveByTagAsync(CourseCacheKeys.CatalogTag(tenantProvider.GetTenant()), cancellationToken);
                    break;

                // ── Asset state/metadata (media pipeline; commands carry no course id) ──
                // Only the readiness entries depend on asset state; evict the coarse
                // tenant-level assets family (readiness entries carry it too).
                case MarkCourseAssetAvailableCommand or MarkCourseAssetFailedCommand or SyncResourceMetadataCommand:
                    await cache.RemoveByTagAsync(CourseCacheKeys.CourseAssetsTag(tenantProvider.GetTenant()), cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache eviction failed for {RequestType}; entries expire within their TTL instead.",
                typeof(TRequest).Name);
        }

        return result;
    }

    private async Task EvictCourseDataAsync(Guid? tenantId, Guid courseId, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            return;
        }

        await cache.RemoveByTagAsync(CourseCacheKeys.CourseTag(tenantId, courseId), cancellationToken);
    }

    private async Task EvictCourseDataAndCatalogAsync(Guid? tenantId, Guid courseId, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            return;
        }

        await cache.RemoveByTagAsync(CourseCacheKeys.CourseTag(tenantId, courseId), cancellationToken);
        await cache.RemoveByTagAsync(CourseCacheKeys.CatalogTag(tenantId), cancellationToken);
    }

    private Task EvictCourseAsync(Guid courseId, CancellationToken cancellationToken) =>
        EvictCourseDataAsync(tenantProvider.GetTenant(), courseId, cancellationToken);

    private Task EvictCourseAndCatalogAsync(Guid courseId, CancellationToken cancellationToken) =>
        EvictCourseDataAndCatalogAsync(tenantProvider.GetTenant(), courseId, cancellationToken);

    private ValueTask EvictSubjectsAsync(CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(CourseCacheKeys.SubjectsTag(tenantProvider.GetTenant()), cancellationToken);
}
