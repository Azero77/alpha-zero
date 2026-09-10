using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Documents.Application.Commands.DeleteDocument;

public record DeleteDocumentCommand(Guid DocumentId) : ICommand<Success>;

public class DeleteDocumentCommandValidator : AbstractValidator<DeleteDocumentCommand>
{
    public DeleteDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
    }
}

public sealed class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, ErrorOr<Success>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IClock _clock;
    private readonly ILogger<DeleteDocumentCommandHandler> _logger;

    public DeleteDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IClock clock,
        ILogger<DeleteDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetFirst(d => d.Id == request.DocumentId, cancellationToken);
        if (document is null)
            return Error.NotFound("Document.NotFound", "Document not found.");

        document.MarkAsDeleted(_clock);
        _logger.LogInformation("Document {DocumentId} marked as deleted.", request.DocumentId);

        return Result.Success;
    }
}
