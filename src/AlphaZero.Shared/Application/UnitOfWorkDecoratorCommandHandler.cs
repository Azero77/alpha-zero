using ErrorOr;
using MediatR;

namespace AlphaZero.Shared.Application;

public class UnitOfWorkDecoratorCommandHandler<TRequest, TResponse> : IPipelineBehavior<TRequest, ErrorOr<TResponse>>
    where TRequest : IRequest<ErrorOr<TResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkDecoratorCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }


    public async Task<ErrorOr<TResponse>> Handle(TRequest request, RequestHandlerDelegate<ErrorOr<TResponse>> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken);

        if (request is not ICommand<TResponse>)
        {
            return result;
        }
        if (!result.IsError)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}


public interface ICommand<TResponse> : IRequest<ErrorOr<TResponse>> { }
