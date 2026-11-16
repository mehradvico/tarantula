using Tarantula.Application.Common;

namespace Tarantula.Application.Abstractions;

/// <summary>A use case that only reads. Same shape as a command handler, kept separate so reads and writes stay distinguishable.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
