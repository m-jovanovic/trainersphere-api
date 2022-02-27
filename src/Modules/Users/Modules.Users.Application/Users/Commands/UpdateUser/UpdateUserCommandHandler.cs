using Application.Messaging;
using Domain.Results;
using Modules.Users.Application.Boundary.Users.Commands.UpdateUser;
using Modules.Users.Domain;
using Modules.Users.Domain.Users;

namespace Modules.Users.Application.Users.Commands.UpdateUser;

/// <summary>
/// Represents the <see cref="UpdateUserCommand"/> handler.
/// </summary>
internal sealed class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserCommandHandler"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public UpdateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken) =>
        await _userRepository.GetByIdAsync(new UserId(request.UserId), cancellationToken)
            .Tap(user => user.ChangeBasicInformation(request.FirstName, request.LastName))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken));
}
