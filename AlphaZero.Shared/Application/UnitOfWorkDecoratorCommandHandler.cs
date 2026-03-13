using MediatR;

namespace AlphaZero.Shared.Application;

public class UnitOfWorkDecoratorCommandHandler<T> : IRequestHandler<T>
    where T : IRequest
{

    IRequestHandler<T> command;
    IUnitOfWork unitOfWork;

    public UnitOfWorkDecoratorCommandHandler(IRequestHandler<T> command, IUnitOfWork unitOfWork)
    {
        this.command = command;
        this.unitOfWork = unitOfWork;
    }

    public async Task Handle(T request, CancellationToken cancellationToken)
    {
        await command.Handle(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
