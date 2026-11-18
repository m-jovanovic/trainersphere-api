using FluentValidation;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Invitations;

internal sealed record InvitationResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Message,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    Guid? ClientId)
{
    public static InvitationResponse From(Invitation invitation, DateTime now) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.FirstName,
            invitation.LastName,
            invitation.Message,
            invitation.StatusAt(now),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.AcceptedAt,
            invitation.ClientId);
}

internal sealed record InvitationPreviewResponse(
    string Email,
    string FirstName,
    string LastName,
    string TrainerName,
    string? Message,
    InvitationStatus Status,
    DateTime ExpiresAt);

internal static class InvitationIssuing
{
    public static async Task PublishAsync(IEventBus eventBus, Invitation invitation, string token, string trainerName, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        await eventBus.PublishAsync(
            new InvitationIssued(
                Ids.New(timeProvider),
                timeProvider.UtcNow(),
                invitation.Id,
                invitation.TrainerId,
                invitation.Email,
                invitation.FirstName,
                invitation.LastName,
                trainerName,
                invitation.Message,
                token,
                invitation.ExpiresAt),
            cancellationToken);
}

internal sealed record CreateInvitationRequest(string Email, string FirstName, string LastName, string? Message);

internal sealed record CreateInvitationCommand(Guid TrainerId, string Email, string FirstName, string LastName, string? Message) : ICommand<InvitationResponse>;

internal sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationCommand>
{
    public const int MessageMaxLength = 500;

    public CreateInvitationValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.Message).MaximumLength(MessageMaxLength);
    }
}

internal sealed class CreateInvitationHandler(
    UserRepository users,
    InvitationRepository invitations,
    IEventBus eventBus,
    TimeProvider timeProvider) : ICommandHandler<CreateInvitationCommand, InvitationResponse>
{
    public async Task<Result<InvitationResponse>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        string email = EmailAddress.Normalize(command.Email);
        DateTime now = timeProvider.UtcNow();

        User? trainer = await users.GetByIdAsync(command.TrainerId, cancellationToken);

        if (trainer is null)
        {
            return UserErrors.NotFound(command.TrainerId);
        }

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return UserErrors.EmailInUse(email);
        }

        if (await invitations.HasActivePendingAsync(command.TrainerId, email, now, cancellationToken))
        {
            return InvitationErrors.PendingExists(email);
        }

        string token = SecureTokens.Create();
        var invitation = Invitation.Issue(Ids.New(timeProvider), command.TrainerId, email, command.FirstName, command.LastName, command.Message, SecureTokens.Hash(token), now);

        await invitations.AddAsync(invitation, cancellationToken);
        await InvitationIssuing.PublishAsync(eventBus, invitation, token, trainer.FullName, timeProvider, cancellationToken);

        return InvitationResponse.From(invitation, now);
    }
}

internal sealed record ResendInvitationCommand(Guid TrainerId, Guid InvitationId) : ICommand<InvitationResponse>;

internal sealed class ResendInvitationHandler(
    UserRepository users,
    InvitationRepository invitations,
    IEventBus eventBus,
    TimeProvider timeProvider) : ICommandHandler<ResendInvitationCommand, InvitationResponse>
{
    public async Task<Result<InvitationResponse>> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();
        string token = SecureTokens.Create();

        Invitation? invitation = await invitations.GetForTrainerAsync(command.InvitationId, command.TrainerId, cancellationToken);
        User? trainer = await users.GetByIdAsync(command.TrainerId, cancellationToken);

        if (invitation is null || trainer is null)
        {
            return InvitationErrors.NotFound;
        }

        return await invitation.Resend(SecureTokens.Hash(token), now)
            .Tap(resent => invitations.UpdateAsync(resent, cancellationToken))
            .Tap(resent => InvitationIssuing.PublishAsync(eventBus, resent, token, trainer.FullName, timeProvider, cancellationToken))
            .Map(resent => InvitationResponse.From(resent, now));
    }
}

internal sealed record CancelInvitationCommand(Guid TrainerId, Guid InvitationId) : ICommand<Unit>;

internal sealed class CancelInvitationHandler(InvitationRepository invitations, IEventBus eventBus, TimeProvider timeProvider) : ICommandHandler<CancelInvitationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CancelInvitationCommand command, CancellationToken cancellationToken)
    {
        Invitation? invitation = await invitations.GetForTrainerAsync(command.InvitationId, command.TrainerId, cancellationToken);

        if (invitation is null)
        {
            return InvitationErrors.NotFound;
        }

        return await invitation.Cancel(timeProvider.UtcNow())
            .Tap(_ => invitations.UpdateAsync(invitation, cancellationToken))
            .Tap(_ => eventBus.PublishAsync(new InvitationCancelled(Ids.New(timeProvider), timeProvider.UtcNow(), invitation.Id, invitation.TrainerId), cancellationToken));
    }
}

internal sealed record ListInvitationsQuery(Guid TrainerId, string? Status, int Page, int PageSize) : IQuery<Paged<InvitationResponse>>;

internal sealed class ListInvitationsValidator : AbstractValidator<ListInvitationsQuery>
{
    public ListInvitationsValidator()
    {
        RuleFor(q => q.Status)
            .Must(status => status is null || KebabCase.TryParse<InvitationStatus>(status, out _))
            .WithMessage($"'{{PropertyName}}' must be one of: {KebabCase.Allowed<InvitationStatus>()}.");
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class ListInvitationsHandler(IDbSession db, TimeProvider timeProvider) : IQueryHandler<ListInvitationsQuery, Paged<InvitationResponse>>
{
    private const string EffectiveStatus = "CASE WHEN status = 'Pending' AND expires_at <= @Now THEN 'Expired' ELSE status END";

    public async Task<Result<Paged<InvitationResponse>>> Handle(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        string? status = query.Status is not null && KebabCase.TryParse(query.Status, out InvitationStatus parsed) ? parsed.ToString() : null;
        DateTime now = timeProvider.UtcNow();

        IReadOnlyList<InvitationRow> rows = await db.QueryAsync<InvitationRow>(
            $"""
            SELECT id, email, first_name, last_name, message, {EffectiveStatus} AS status,
                   created_at, expires_at, accepted_at, client_id, count(*) OVER () AS total_count
            FROM identity.invitations
            WHERE trainer_id = @TrainerId AND (@Status::text IS NULL OR {EffectiveStatus} = @Status::text)
            ORDER BY created_at DESC, id DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new { query.TrainerId, Status = status, Now = now, query.PageSize, Offset = (query.Page - 1) * query.PageSize },
            cancellationToken);

        int total = rows.Count > 0 ? (int)rows[0].TotalCount : await CountAsync(query.TrainerId, status, now, cancellationToken);

        return new Paged<InvitationResponse>(
            [.. rows.Select(r => new InvitationResponse(r.Id, r.Email, r.FirstName, r.LastName, r.Message, Enum.Parse<InvitationStatus>(r.Status), r.CreatedAt, r.ExpiresAt, r.AcceptedAt, r.ClientId))],
            query.Page,
            query.PageSize,
            total);
    }

    private Task<int> CountAsync(Guid trainerId, string? status, DateTime now, CancellationToken cancellationToken) =>
        db.QuerySingleAsync<int>(
            $"SELECT count(*)::int FROM identity.invitations WHERE trainer_id = @TrainerId AND (@Status::text IS NULL OR {EffectiveStatus} = @Status::text)",
            new { TrainerId = trainerId, Status = status, Now = now },
            cancellationToken);

    internal sealed record InvitationRow
    {
        public Guid Id { get; init; }

        public string Email { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string? Message { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime ExpiresAt { get; init; }

        public DateTime? AcceptedAt { get; init; }

        public Guid? ClientId { get; init; }

        public long TotalCount { get; init; }
    }
}

internal sealed record GetInvitationPreviewQuery(string Token) : IQuery<InvitationPreviewResponse>;

internal sealed class GetInvitationPreviewHandler(InvitationRepository invitations, UserRepository users, TimeProvider timeProvider)
    : IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewResponse>
{
    public async Task<Result<InvitationPreviewResponse>> Handle(GetInvitationPreviewQuery query, CancellationToken cancellationToken)
    {
        Invitation? invitation = await invitations.GetByTokenHashAsync(SecureTokens.Hash(query.Token), cancellationToken);
        User? trainer = invitation is null ? null : await users.GetByIdAsync(invitation.TrainerId, cancellationToken);

        if (invitation is null || trainer is null)
        {
            return InvitationErrors.NotFound;
        }

        return new InvitationPreviewResponse(
            invitation.Email,
            invitation.FirstName,
            invitation.LastName,
            trainer.FullName,
            invitation.Message,
            invitation.StatusAt(timeProvider.UtcNow()),
            invitation.ExpiresAt);
    }
}

internal sealed record AcceptInvitationRequest(string FirstName, string LastName, string Password, string? TimeZone);

internal sealed record AcceptInvitationCommand(string Token, string FirstName, string LastName, string Password, string? TimeZone) : ICommand<AuthSession>;

internal sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.Password).ValidPassword();
        RuleFor(c => c.TimeZone).ValidTimeZone();
    }
}

internal sealed class AcceptInvitationHandler(
    InvitationRepository invitations,
    UserRepository users,
    PasswordHasher hasher,
    AuthSessionIssuer sessions,
    IEventBus eventBus,
    TimeProvider timeProvider) : ICommandHandler<AcceptInvitationCommand, AuthSession>
{
    public async Task<Result<AuthSession>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();
        Invitation? invitation = await invitations.GetByTokenHashAsync(SecureTokens.Hash(command.Token), cancellationToken);

        if (invitation is null)
        {
            return InvitationErrors.NotFound;
        }

        var client = User.RegisterClient(
            Ids.New(timeProvider),
            invitation.TrainerId,
            invitation.Email,
            hasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            command.TimeZone,
            now);

        Result<Invitation> accepted = invitation.Accept(client.Id, now);

        if (accepted.Value is Error error)
        {
            return error;
        }

        if (!await users.TryAddAsync(client, cancellationToken))
        {
            return UserErrors.EmailInUse(invitation.Email);
        }

        await invitations.UpdateAsync(invitation, cancellationToken);
        await eventBus.PublishAsync(
            new ClientRegistered(Ids.New(timeProvider), now, client.Id, invitation.TrainerId, invitation.Id, client.Email, client.FirstName, client.LastName, client.TimeZone),
            cancellationToken);

        return await sessions.IssueAsync(client, familyId: null, cancellationToken);
    }
}

internal sealed class InvitationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder trainer = app.MapGroup("/api/invitations")
            .WithTags("Invitations")
            .RequirePermission(Permissions.InvitationsManage);

        trainer.MapGet(
                string.Empty,
                (string? status, int? page, int? pageSize, IQueryHandler<ListInvitationsQuery, Paged<InvitationResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListInvitationsQuery(http.User.GetUserId(), status, page ?? 1, pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ToOk())
            .WithName("ListInvitations");

        trainer.MapPost(
                string.Empty,
                async (CreateInvitationRequest request, ICommandHandler<CreateInvitationCommand, InvitationResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    (await handler.Handle(new CreateInvitationCommand(http.User.GetUserId(), request.Email, request.FirstName, request.LastName, request.Message), cancellationToken))
                        .ToHttpResult(invitation => TypedResults.Created($"/api/invitations/{invitation.Id}", invitation)))
            .WithName("CreateInvitation");

        trainer.MapPost(
                "/{id:guid}/resend",
                (Guid id, ICommandHandler<ResendInvitationCommand, InvitationResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ResendInvitationCommand(http.User.GetUserId(), id), cancellationToken).ToOk())
            .WithName("ResendInvitation");

        trainer.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<CancelInvitationCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new CancelInvitationCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("CancelInvitation");

        RouteGroupBuilder open = app.MapGroup("/api/invitations")
            .WithTags("Invitations")
            .AllowAnonymous();

        open.MapGet(
                "/{token}",
                (string token, IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewResponse> handler, CancellationToken cancellationToken) =>
                    handler.Handle(new GetInvitationPreviewQuery(token), cancellationToken).ToOk())
            .WithName("GetInvitationPreview");

        open.MapPost(
                "/{token}/accept",
                async (string token, AcceptInvitationRequest request, ICommandHandler<AcceptInvitationCommand, AuthSession> handler, RefreshCookies cookies, HttpContext http, CancellationToken cancellationToken) =>
                    (await handler.Handle(new AcceptInvitationCommand(token, request.FirstName, request.LastName, request.Password, request.TimeZone), cancellationToken))
                        .ToHttpResult(session =>
                        {
                            cookies.Append(http, session.RefreshToken, session.RefreshTokenExpiresAt);
                            return TypedResults.Created("/api/auth/me", session.Response);
                        }))
            .WithName("AcceptInvitation")
            .RequireRateLimiting(HostingExtensions.AuthRateLimitPolicy);
    }
}
