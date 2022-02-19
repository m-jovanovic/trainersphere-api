using Application.Messaging;
using Domain.Results;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;
using Modules.Users.Domain;
using Modules.Users.Domain.Users;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="RegisterUserCommand"/> handler.
/// </summary>
internal sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserCommandHandler"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public RegisterUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken) =>
        await User.Create(
                Guid.NewGuid(),
                request.Email,
                request.FirstName,
                request.LastName)
            .Tap(user => _userRepository.Add(user))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken))
            .Map(user => user.Id);
}
