using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.SharedKernel.Messaging;

/// <summary>
/// A request that changes state. Command handlers run inside a database transaction.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "Marker interface: the type parameter ties the request to its response type.")]
public interface ICommand<TResponse>
    where TResponse : notnull;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// A request that only reads state.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "Marker interface: the type parameter ties the request to its response type.")]
public interface IQuery<TResponse>
    where TResponse : notnull;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
