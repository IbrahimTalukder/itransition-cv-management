using System.Text.RegularExpressions;

namespace CvManagementSystem;

public static class DisplayFormat
{
    public static string Humanize(object value) =>
        Regex.Replace(value.ToString() ?? "", "(?<!^)([A-Z])", " $1");
}
