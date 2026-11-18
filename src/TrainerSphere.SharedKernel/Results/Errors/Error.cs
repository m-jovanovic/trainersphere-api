namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The failure case of <see cref="Result{T}"/>. The hierarchy is closed: only this assembly can derive from it,
/// so switches over <see cref="Error"/> are checked for exhaustiveness by the compiler.
/// </summary>
/// <param name="Code">A stable, machine-readable code such as <c>Trainer.NotFound</c>.</param>
/// <param name="Message">A human-readable description.</param>
public closed record Error(string Code, string Message);
