using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record CreateVideoCourseAssetCommand(
    Guid VideoId,
    Guid TenantId,
    Guid CourseId,
    string Title
) : ICommand<Success>;

public class CreateVideoCourseAssetCommandValidator : AbstractValidator<CreateVideoCourseAssetCommand>
{
    public CreateVideoCourseAssetCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
    }
}

public sealed class CreateVideoCourseAssetCommandHandler 
    : IRequestHandler<CreateVideoCourseAssetCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;

    public CreateVideoCourseAssetCommandHandler(IRepository<CourseAsset> assetRepository)
        => _assetRepository = assetRepository;

    public async Task<ErrorOr<Success>> Handle(
        CreateVideoCourseAssetCommand request, CancellationToken ct)
    {
        // Idempotency: check if asset already exists
        var existing = await _assetRepository.GetById(request.VideoId, ct);
        if (existing is not null)
            return Result.Success;

        var videoArn = ResourceArn.ForVideo(request.TenantId, request.VideoId);
        var assetResult = VideoCourseAsset.Create(
            request.VideoId, request.TenantId, videoArn, request.Title, request.CourseId);

        if (assetResult.IsError) return assetResult.Errors;

        _assetRepository.Add(assetResult.Value);
        return Result.Success;
    }
}
