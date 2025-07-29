using System.Text.RegularExpressions;

namespace Core.Utilities.Extensions
{
    public static class StringExtension
    {
        /// <summary>
        /// Converts strings like variable names to display format.
        /// For Example: variableName => Variable Name, other_name => Other Name
        /// </summary>
        /// <param name="name">String to convert</param>
        /// <returns>To display string</returns>
        public static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var withSpaces = Regex.Replace(name, "(?<!^)([A-Z])", " $1");
            return char.ToUpper(withSpaces[0]) + withSpaces.Substring(1);
        }
    }
}