using System.Text.RegularExpressions;

namespace Core.Utilities.Extensions
{
    public class StringExtension
    {
        public static string Prettify(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return string.Empty;

            var withSpaces = Regex.Replace(variableName, "(?<!^)([A-Z])", " $1");
            return char.ToUpper(withSpaces[0]) + withSpaces.Substring(1);
        }
    }
}