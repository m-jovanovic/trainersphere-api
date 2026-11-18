using TrainerSphere.IntegrationTests.Contracts;

namespace TrainerSphere.IntegrationTests.Builders;

public sealed class ExerciseBuilder
{
    private string _name = Unique.Name("Exercise");
    private string _category = "strength";
    private string _movementPattern = "squat";
    private string[] _primaryMuscles = ["quads"];
    private string[] _secondaryMuscles = ["glutes"];
    private string[] _equipment = ["barbell"];
    private bool _unilateral;
    private string? _cue;

    public ExerciseBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ExerciseBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    public ExerciseBuilder WithMovementPattern(string movementPattern)
    {
        _movementPattern = movementPattern;
        return this;
    }

    public ExerciseBuilder WithPrimaryMuscles(params string[] muscles)
    {
        _primaryMuscles = muscles;
        return this;
    }

    public ExerciseBuilder WithSecondaryMuscles(params string[] muscles)
    {
        _secondaryMuscles = muscles;
        return this;
    }

    public ExerciseBuilder WithEquipment(params string[] equipment)
    {
        _equipment = equipment;
        return this;
    }

    public ExerciseBuilder WithUnilateral(bool unilateral = true)
    {
        _unilateral = unilateral;
        return this;
    }

    public ExerciseBuilder WithCue(string? cue)
    {
        _cue = cue;
        return this;
    }

    public ExerciseWrite Build() => new(_name, _category, _movementPattern, _primaryMuscles, _secondaryMuscles, _equipment, _unilateral, _cue);
}

public sealed class WorkoutBuilder
{
    private readonly List<WorkoutExerciseWrite> _exercises = [];
    private string _name = Unique.Name("Workout");
    private string? _description = "A test workout.";

    public WorkoutBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorkoutBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public WorkoutBuilder WithExercise(WorkoutExerciseWrite exercise)
    {
        _exercises.Add(exercise);
        return this;
    }

    public WorkoutBuilder WithExercises(params WorkoutExerciseWrite[] exercises)
    {
        _exercises.AddRange(exercises);
        return this;
    }

    public WorkoutWrite Build() => new(_name, _description, _exercises);
}

public sealed class WorkoutExerciseBuilder
{
    private readonly Guid _exerciseId;
    private int _sets = 3;
    private GoalWrite _goal = new("reps", 8, 12);
    private LoadWrite _load = new("weight", 60m);
    private int _restSeconds = 90;
    private string? _tempo;
    private string? _supersetGroup;
    private string? _notes;

    public WorkoutExerciseBuilder(Guid exerciseId) => _exerciseId = exerciseId;

    public WorkoutExerciseBuilder WithSets(int sets)
    {
        _sets = sets;
        return this;
    }

    public WorkoutExerciseBuilder WithGoal(string type, int? min = null, int? max = null)
    {
        _goal = new GoalWrite(type, min, max);
        return this;
    }

    public WorkoutExerciseBuilder WithLoad(string type, decimal? value = null)
    {
        _load = new LoadWrite(type, value);
        return this;
    }

    public WorkoutExerciseBuilder WithRestSeconds(int restSeconds)
    {
        _restSeconds = restSeconds;
        return this;
    }

    public WorkoutExerciseBuilder WithTempo(string? tempo)
    {
        _tempo = tempo;
        return this;
    }

    public WorkoutExerciseBuilder WithSupersetGroup(string? supersetGroup)
    {
        _supersetGroup = supersetGroup;
        return this;
    }

    public WorkoutExerciseBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public WorkoutExerciseWrite Build() => new(_exerciseId, _sets, _goal, _load, _restSeconds, _tempo, _supersetGroup, _notes);
}

public sealed class ProgramBuilder
{
    private readonly List<ProgramSlotWrite> _slots = [];
    private string _name = Unique.Name("Program");
    private string? _description = "A test program.";
    private int _weeks = 4;

    public ProgramBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProgramBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public ProgramBuilder WithWeeks(int weeks)
    {
        _weeks = weeks;
        return this;
    }

    public ProgramBuilder WithSlot(int week, int day, Guid workoutId)
    {
        _slots.Add(new ProgramSlotWrite(week, day, workoutId));
        return this;
    }

    public ProgramWrite Build() => new(_name, _description, _weeks, _slots);
}

public sealed class AssignmentBuilder
{
    private readonly Guid _clientId;
    private readonly Guid _workoutId;
    private string _date = TrainingDates.Today;
    private string? _notes;

    public AssignmentBuilder(Guid clientId, Guid workoutId) => (_clientId, _workoutId) = (clientId, workoutId);

    public AssignmentBuilder On(string date)
    {
        _date = date;
        return this;
    }

    public AssignmentBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public AssignmentWrite Build() => new(_clientId, _workoutId, _date, _notes);
}

public sealed class SetBuilder
{
    private readonly Guid _exerciseId;
    private readonly int _position;
    private int _setNumber = 1;
    private decimal? _weightKg = 60m;
    private int? _reps = 8;
    private int? _durationSeconds;
    private int? _distanceMeters;
    private decimal? _rpe;
    private bool _completed = true;
    private string? _notes;

    public SetBuilder(Guid exerciseId, int position = 1) => (_exerciseId, _position) = (exerciseId, position);

    public SetBuilder WithSetNumber(int setNumber)
    {
        _setNumber = setNumber;
        return this;
    }

    public SetBuilder WithWeightKg(decimal? weightKg)
    {
        _weightKg = weightKg;
        return this;
    }

    public SetBuilder WithReps(int? reps)
    {
        _reps = reps;
        return this;
    }

    public SetBuilder WithDurationSeconds(int? durationSeconds)
    {
        _durationSeconds = durationSeconds;
        return this;
    }

    public SetBuilder WithDistanceMeters(int? distanceMeters)
    {
        _distanceMeters = distanceMeters;
        return this;
    }

    public SetBuilder WithRpe(decimal? rpe)
    {
        _rpe = rpe;
        return this;
    }

    public SetBuilder WithCompleted(bool completed)
    {
        _completed = completed;
        return this;
    }

    public SetBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public SetWrite Build() => new(_exerciseId, _position, _setNumber, _weightKg, _reps, _durationSeconds, _distanceMeters, _rpe, _completed, _notes);
}

public sealed class BodyMetricBuilder
{
    private decimal? _weightKg = 80m;
    private decimal? _bodyFatPercent;
    private decimal? _waistCm;
    private decimal? _chestCm;
    private decimal? _hipsCm;
    private decimal? _armCm;
    private decimal? _thighCm;
    private int? _restingHeartRate;
    private string? _notes;

    public BodyMetricBuilder WithWeightKg(decimal? weightKg)
    {
        _weightKg = weightKg;
        return this;
    }

    public BodyMetricBuilder WithBodyFatPercent(decimal? bodyFatPercent)
    {
        _bodyFatPercent = bodyFatPercent;
        return this;
    }

    public BodyMetricBuilder WithWaistCm(decimal? waistCm)
    {
        _waistCm = waistCm;
        return this;
    }

    public BodyMetricBuilder WithChestCm(decimal? chestCm)
    {
        _chestCm = chestCm;
        return this;
    }

    public BodyMetricBuilder WithHipsCm(decimal? hipsCm)
    {
        _hipsCm = hipsCm;
        return this;
    }

    public BodyMetricBuilder WithArmCm(decimal? armCm)
    {
        _armCm = armCm;
        return this;
    }

    public BodyMetricBuilder WithThighCm(decimal? thighCm)
    {
        _thighCm = thighCm;
        return this;
    }

    public BodyMetricBuilder WithRestingHeartRate(int? restingHeartRate)
    {
        _restingHeartRate = restingHeartRate;
        return this;
    }

    public BodyMetricBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public BodyMetricBuilder WithoutMeasurements()
    {
        _weightKg = null;
        return this;
    }

    public BodyMetricWrite Build() =>
        new(_weightKg, _bodyFatPercent, _waistCm, _chestCm, _hipsCm, _armCm, _thighCm, _restingHeartRate, _notes);
}
