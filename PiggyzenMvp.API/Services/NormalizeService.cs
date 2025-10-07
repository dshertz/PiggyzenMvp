using System.Text.RegularExpressions;

namespace PiggyzenMvp.API.Services
{
    public class NormalizeService
    {
        public string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var text = input.Trim().ToLowerInvariant();

            // 1. Ersätt specialtecken med mellanslag
            text = Regex.Replace(text, @"[,.:;()*_\""-]", " ");

            // 2. Ersätt & med "och"
            text = text.Replace("&", "och");

            // 3. Ta bort apostrofer
            text = Regex.Replace(text, "[’']", "");

            // 4. Ersätt svenska bokstäver
            text = Regex.Replace(text, "[åäÅÄ]", "a");
            text = Regex.Replace(text, "[öÖ]", "o");

            // 5. Normalisera mellanslag (valfritt)
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
    }
}
