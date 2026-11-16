using Tarantula.Application.Common;

namespace Tarantula.Application.Abstractions;

/// <summary>One use case: takes a command, returns a <see cref="Result{T}"/>. No framework types inside.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
