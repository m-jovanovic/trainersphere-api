using TrainerSphere.IntegrationTests.Contracts;

namespace TrainerSphere.IntegrationTests.Builders;

public sealed class SignupBuilder
{
    private string _email = Unique.Email("trainer");
    private string _password = "Trainer-pass-1";
    private string _firstName = "Maya";
    private string _lastName = Unique.Name("Novak");
    private string? _timeZone = "Europe/Belgrade";

    public SignupBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public SignupBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public SignupBuilder WithName(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }

    public SignupBuilder WithTimeZone(string? timeZone)
    {
        _timeZone = timeZone;
        return this;
    }

    public SignupRequest Build() => new(_email, _password, _firstName, _lastName, _timeZone);
}

public sealed class InvitationBuilder
{
    private string _email = Unique.Email("client");
    private string _firstName = "Alex";
    private string _lastName = Unique.Name("Rivera");
    private string? _message = "Excited to work with you!";

    public InvitationBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public InvitationBuilder WithName(string firstName, string lastName)
    {
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }

    public InvitationBuilder WithMessage(string? message)
    {
        _message = message;
        return this;
    }

    public CreateInvitationRequest Build() => new(_email, _firstName, _lastName, _message);
}

public sealed class AcceptInvitationBuilder
{
    private string _firstName = "Alex";
    private string _lastName = "Rivera";
    private string _password = "Client-pass-1";
    private string? _timeZone = "Europe/Belgrade";

    public AcceptInvitationBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public AcceptInvitationBuilder WithLastName(string lastName)
    {
        _lastName = lastName;
        return this;
    }

    public AcceptInvitationBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public AcceptInvitationBuilder WithTimeZone(string? timeZone)
    {
        _timeZone = timeZone;
        return this;
    }

    public AcceptInvitationRequest Build() => new(_firstName, _lastName, _password, _timeZone);
}
