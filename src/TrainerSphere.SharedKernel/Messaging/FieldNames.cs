using System.Text;

namespace TrainerSphere.SharedKernel.Messaging;

public static class FieldNames
{
    /// <summary>
    /// Converts a property path such as <c>Exercises[0].Sets</c> to the camelCase JSON path <c>exercises[0].sets</c>.
    /// </summary>
    public static string ToJsonPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return string.Empty;
        }

        StringBuilder builder = new(propertyPath.Length);
        bool segmentStart = true;

        foreach (char c in propertyPath)
        {
            builder.Append(segmentStart && char.IsUpper(c) ? char.ToLowerInvariant(c) : c);
            segmentStart = c == '.';
        }

        return builder.ToString();
    }
}
