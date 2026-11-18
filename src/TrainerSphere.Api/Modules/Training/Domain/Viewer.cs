namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// The authenticated caller as seen by Training. <see cref="TrainerScope"/> is the trainer whose custom exercises
/// the caller can see: the trainer themself, or a client's trainer.
/// </summary>
internal sealed record Viewer(Guid UserId, bool IsTrainer, Guid? TrainerScope)
{
    public bool IsClient => !IsTrainer;

    public RecordedBy Role => IsTrainer ? RecordedBy.Trainer : RecordedBy.Client;

    public static Viewer Trainer(Guid trainerId) => new(trainerId, IsTrainer: true, trainerId);

    public static Viewer Client(Guid clientId, Guid? trainerId) => new(clientId, IsTrainer: false, trainerId);
}
