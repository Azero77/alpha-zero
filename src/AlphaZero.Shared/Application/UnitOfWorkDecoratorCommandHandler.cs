using ErrorOr;
using MediatR;

namespace AlphaZero.Shared.Application;

public class UnitOfWorkDecoratorCommandHandler<T> : IRequestHandler<T>
    where T : IRequest
{
    private readonly IRequestHandler<T> _command;
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkDecoratorCommandHandler(IRequestHandler<T> command, IUnitOfWork unitOfWork)
    {
        _command = command;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(T request, CancellationToken cancellationToken)
    {
        await _command.Handle(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public class UnitOfWorkDecoratorCommandHandler<TRequest, TResponse> : IRequestHandler<TRequest, ErrorOr<TResponse>>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    private readonly IRequestHandler<TRequest, ErrorOr<TResponse>> _inner;
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkDecoratorCommandHandler(IRequestHandler<TRequest, ErrorOr<TResponse>> inner, IUnitOfWork unitOfWork)
    {
        _inner = inner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ErrorOr<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var result = await _inner.Handle(request, cancellationToken);

        if (!result.IsError)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
