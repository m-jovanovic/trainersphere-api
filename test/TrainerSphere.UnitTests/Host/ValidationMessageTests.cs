using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Resources;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Training.Data;

namespace TrainerSphere.UnitTests.Host;

/// <summary>
/// Messages sit next to the field they are keyed by, so none may name a property the way C# spells it
/// (<c>'Page Size' must ...</c>). Other fields are referred to by their camelCase JSON name.
/// </summary>
public sealed partial class ValidationMessageTests
{
    public static TheoryData<string> DefaultMessageKeys =>
    [
        .. typeof(IPropertyValidator).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPropertyValidator).IsAssignableFrom(t))
            .Select(t => t.Name.Split('`')[0])
            .Distinct(StringComparer.Ordinal)
            .Where(key => !string.IsNullOrEmpty(new LanguageManager().GetString(key, CultureInfo.GetCultureInfo("en")))),
    ];

    public static TheoryData<Type> Validators =>
    [
        .. typeof(DemoAccounts).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && typeof(IValidator).IsAssignableFrom(t)),
    ];

    [Theory]
    [MemberData(nameof(DefaultMessageKeys))]
    public void BuiltInMessage_DoesNotNameTheProperty(string key)
    {
        RuntimeHelpers.RunModuleConstructor(typeof(DemoAccounts).Module.ModuleHandle);

        string message = ValidatorOptions.Global.LanguageManager.GetString(key);

        message.ShouldNotContain("{PropertyName}", Case.Sensitive, message);
        message.ShouldNotContain("{PropertyValue}", Case.Sensitive, message);
        Offence(typeof(object), message).ShouldBeNull(message);
    }

    [Theory]
    [MemberData(nameof(Validators))]
    public void ValidatorMessages_DoNotNamePropertiesInPascalCase(Type validatorType)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(ExerciseLibrary.LoadEmbedded())
            .BuildServiceProvider();
        var validator = (IValidator)ActivatorUtilities.CreateInstance(services, validatorType);

        List<(Type Model, string Message)> messages = [];
        Collect(validator, messages);

        object model = RuntimeHelpers.GetUninitializedObject(validator.CreateDescriptor().GetType().GetGenericArguments()[0]);
        try
        {
            ValidationContext<object> context = new(model);
            messages.AddRange(validator.Validate(context).Errors.Select(f => (model.GetType(), f.ErrorMessage)));
        }
        catch (Exception exception) when (exception is NullReferenceException or ArgumentNullException)
        {
            // A rule that dereferences a required member without a guard; the static walk above still covers it.
        }

        messages.ShouldNotBeEmpty();
        foreach ((Type modelType, string message) in messages)
        {
            Offence(modelType, message).ShouldBeNull($"{validatorType.Name}: \"{message}\"");
        }
    }

    private static void Collect(IValidator validator, List<(Type Model, string Message)> messages)
    {
        Type model = validator.CreateDescriptor().GetType().GetGenericArguments()[0];

        foreach (IValidationRule rule in validator.CreateDescriptor().Rules)
        {
            foreach (IRuleComponent component in rule.Components)
            {
                if (component.Validator is IChildValidatorAdaptor && ChildOf(component.Validator, model) is { } child)
                {
                    Collect(child, messages);
                    continue;
                }

                if (TemplateOf(component, model) is { } template)
                {
                    messages.Add((model, template));
                }
            }
        }
    }

    private static IValidator? ChildOf(IPropertyValidator adaptor, Type model)
    {
        MethodInfo? getValidator = adaptor.GetType().GetMethod("GetValidator", BindingFlags.Instance | BindingFlags.Public);
        return getValidator?.Invoke(adaptor, [EmptyContext(model), null]) as IValidator;
    }

    /// <summary>
    /// A message built by a lambda cannot be read without an instance, so it is built for an empty one.
    /// </summary>
    private static string? TemplateOf(IRuleComponent component, Type model)
    {
        try
        {
            return component.GetUnformattedErrorMessage();
        }
        catch (NullReferenceException)
        {
            FieldInfo factory = component.GetType().GetField("_errorMessageFactory", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("FluentValidation no longer keeps message factories in _errorMessageFactory.");
            var build = (Delegate)factory.GetValue(component)!;
            Type valueType = build.Method.GetParameters()[^1].ParameterType;
            return (string?)build.DynamicInvoke(EmptyContext(model), valueType.IsValueType ? Activator.CreateInstance(valueType) : null);
        }
    }

    private static object EmptyContext(Type model) =>
        Activator.CreateInstance(typeof(ValidationContext<>).MakeGenericType(model), RuntimeHelpers.GetUninitializedObject(model))!;

    private static string? Offence(Type model, string message)
    {
        string text = Placeholder().Replace(message, string.Empty);

        if (message.Contains("{PropertyName}", StringComparison.Ordinal))
        {
            return "uses the {PropertyName} placeholder";
        }

        if (QuotedPascalCase().IsMatch(text))
        {
            return "quotes a PascalCase name; quote the camelCase JSON name instead";
        }

        string? named = model.GetProperties()
            .Select(p => p.Name)
            .FirstOrDefault(name => Regex.IsMatch(text, $@"\b({name}|{SplitWords().Replace(name, " $1")})\b", RegexOptions.None, TimeSpan.FromSeconds(1)));

        return named is null ? null : $"names the property '{named}'";
    }

    [GeneratedRegex("{[A-Za-z]+}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("'[A-Z]")]
    private static partial Regex QuotedPascalCase();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex SplitWords();
}
