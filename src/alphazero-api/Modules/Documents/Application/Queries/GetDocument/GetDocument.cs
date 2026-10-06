using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Repositories;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Documents.Application.Queries.GetDocument;

public record GetDocumentQuery(Guid DocumentId) : IRequest<ErrorOr<DocumentDetailsResponse>>;

public record DocumentDetailsResponse(
    Guid Id,
    string Arn,
    string Title,
    string? Description,
    string FileType,
    long FileSizeBytes,
    DateTime CreatedOn);

public class GetDocumentQueryValidator : AbstractValidator<GetDocumentQuery>
{
    public GetDocumentQueryValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, ErrorOr<DocumentDetailsResponse>>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<ErrorOr<DocumentDetailsResponse>> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetFirst(d => d.Id == request.DocumentId, cancellationToken);
        if (document is null)
            return Error.NotFound("Document.NotFound", "Document not found.");

        return new DocumentDetailsResponse(
            document.Id,
            document.Arn.Value,
            document.Title,
            document.Description,
            document.FileType,
            document.FileSizeBytes,
            document.CreatedOn);
    }
}
