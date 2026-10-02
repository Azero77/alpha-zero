using ErrorOr;
using MediatR;

namespace AlphaZero.Shared.Application;

public interface IQuery<TResponse> : IRequest<ErrorOr<TResponse>> { }
