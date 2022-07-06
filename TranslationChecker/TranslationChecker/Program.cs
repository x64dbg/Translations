using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace TranslationChecker
{
    // https://json2csharp.com/code-converters/xml-to-csharp

    [XmlRoot(ElementName = "translation")]
    public class Translation
    {
        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlElement(ElementName = "numerusform")]
        public List<string> Numerusforms { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "location")]
    public class Location
    {
        [XmlAttribute(AttributeName = "filename")]
        public string Filename { get; set; }

        [XmlAttribute(AttributeName = "line")]
        public int Line { get; set; }
    }

    [XmlRoot(ElementName = "message")]
    public class Message
    {
        [XmlAttribute(AttributeName = "numerus")]
        public string Numerus { get; set; }

        [XmlElement(ElementName = "location")]
        public List<Location> Locations { get; set; }

        [XmlElement(ElementName = "source")]
        public string Source { get; set; }

        [XmlElement(ElementName = "translation")]
        public Translation Translation { get; set; }
    }

    [XmlRoot(ElementName = "context")]
    public class Context
    {
        [XmlElement(ElementName = "name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "message")]
        public List<Message> Messages { get; set; }
    }

    [XmlRoot(ElementName = "TS")]
    public class QtTranslations
    {
        [XmlElement(ElementName = "context")]
        public List<Context> Contexts { get; set; }

        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "language")]
        public string Language { get; set; }

        [XmlAttribute(AttributeName = "sourcelanguage")]
        public string Sourcelanguage { get; set; }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return new UTF8Encoding(false); } }
    }

    internal class Program
    {
        /// <summary>
        /// Checks if the file serializes back from deserialized form without any differences.
        /// </summary>
        static bool CheckSerialize(string tsFile)
        {
            var utf8 = new UTF8Encoding(false);
            var xml = File.ReadAllText(tsFile, utf8);

            XmlSerializer serializer = new XmlSerializer(typeof(QtTranslations));
            using (var reader = new StringReader(xml))
            {
                var test = (QtTranslations)serializer.Deserialize(reader);
                using (var writer = new Utf8StringWriter())
                using (var xw = XmlWriter.Create(writer, new XmlWriterSettings
                {
                    Indent = true,
                }))
                {
                    xw.WriteDocType("TS", null, null, null);
                    var ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    serializer.Serialize(xw, test, ns);
                    var xml2 = writer.ToString().Replace(" />", "/>").Replace("<!DOCTYPE TS >", "<!DOCTYPE TS>").Replace("\r", "") + "\n";
                    if (xml2 != xml)
                    {
                        File.WriteAllText(tsFile + ".serialized", xml2, utf8);
                        Console.WriteLine($"Serialization error with file: {tsFile}");
                        return false;
                    }
                }
            }
            return true;
        }

        // References:
        // - https://en.cppreference.com/w/cpp/io/c/fprintf
        // - https://docs.microsoft.com/en-us/cpp/c-runtime-library/format-specification-syntax-printf-and-wprintf-functions?view=msvc-170#flags
        static Regex FormatSpecifierRegex = new Regex(@"(%[-+ #]*(\d+|\*)?(.\d+|.\*)?(hh|h|l|ll|j|z|t|L|I|I32|I64|w)?[csdioxXufFeEaAgGp]|%n|%%)");

        static Regex QtArgumentRegex = new Regex(@"(%\d+)");

        static bool CheckTranslation(string original, string translation)
        {
            // This checks for context menu keys, disable for now because there is a lot of noise
#if false
            if (original.Count(c => c == '&') != translation.Count(c => c == '&'))
                return false;
#endif

            // Check if original has the same format strings as the translation
            var originalMatches = FormatSpecifierRegex.Matches(original);
            var translationMatches = FormatSpecifierRegex.Matches(translation);
            if (originalMatches.Count != translationMatches.Count)
            {
                return false;
            }
            for (var i = 0; i < originalMatches.Count; i++)
            {
                var originalMatch = originalMatches[i];
                var translationMatch = translationMatches[i];
                if (originalMatch.ToString() != translationMatch.ToString())
                    return false;
            }
            if (originalMatches.Count == 0)
            {
                originalMatches = QtArgumentRegex.Matches(original);
                translationMatches = QtArgumentRegex.Matches(translation);
                string SortedArgs(MatchCollection matches)
                {
                    var sorted = new List<string>();
                    for (var i = 0; i < matches.Count; i++)
                        sorted.Add(matches[i].ToString());
                    sorted.Sort();
                    return string.Join(" ", sorted);
                }
                return SortedArgs(originalMatches) == SortedArgs(translationMatches);
            }
            return true;
        }

        static string FixTranslation(string original, string translation)
        {
            // TODO: try to fix almost-correct format strings (like %hello -> %shello)
            return original;
        }

        static int ErrorCount = 0;

        static bool CheckFile(string tsFile, bool fix)
        {
            if (!CheckSerialize(tsFile))
            {
                return false;
            }

            var utf8 = new UTF8Encoding(false);
            var xml = File.ReadAllText(tsFile, utf8);
            var success = true;

            void ReportError(Message message, string translation)
            {
                var location = message.Locations.First();
                Console.WriteLine($"  Format string error ({location.Filename}:{location.Line})\n    Source:\n      '{message.Source}'\n    Translation:\n      '{translation}'");
                success = false;
                ErrorCount++;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(QtTranslations));
            using (var reader = new StringReader(xml))
            {
                var ts = (QtTranslations)serializer.Deserialize(reader);
                var url = $"https://crowdin.com/translate/x64dbg/1/en-{ts.Language.ToLower().Replace("-", "")}";
                Console.WriteLine($"Checking {tsFile} ({LanguageCodes.GetLanguageName(ts.Language)}) => {url}");

                foreach (var context in ts.Contexts)
                {
                    foreach (var message in context.Messages)
                    {
                        var original = message.Source;
                        if (message.Translation.Type == "unfinished")
                        {
                            continue;
                        }

                        if (message.Numerus == "yes")
                        {
                            for (var i = 0; i < message.Translation.Numerusforms.Count; i++)
                            {
                                var translation = message.Translation.Numerusforms[i];
                                if (!CheckTranslation(original, translation))
                                {
                                    ReportError(message, translation);
                                    if (fix)
                                        message.Translation.Numerusforms[i] = FixTranslation(original, translation);
                                }
                            }
                        }
                        else
                        {
                            var translation = message.Translation.Text;
                            if (!CheckTranslation(original, translation))
                            {
                                ReportError(message, translation);
                                if (fix)
                                    message.Translation.Text = FixTranslation(original, translation);
                            }
                        }
                    }
                }

                if (!success && fix)
                {
                    using (var writer = new Utf8StringWriter())
                    using (var xw = XmlWriter.Create(writer, new XmlWriterSettings
                    {
                        Indent = true,
                    }))
                    {
                        xw.WriteDocType("TS", null, null, null);
                        var ns = new XmlSerializerNamespaces();
                        ns.Add("", "");
                        serializer.Serialize(xw, ts, ns);
                        var xml2 = writer.ToString().Replace(" />", "/>").Replace("<!DOCTYPE TS >", "<!DOCTYPE TS>").Replace("\r", "") + "\n";
                        File.WriteAllText(tsFile, xml2, utf8);
                    }
                }
            }

            return success;
        }

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: TranslationChecker x64dbg.ts [--fix]");
                return 1;
            }

            var fix = false;
            var folder = false;
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--fix")
                    fix = true;
                else if (args[i] == "--folder")
                    folder = true;
            }

            var success = true;
            if (folder)
            {
                foreach (var tsFile in Directory.EnumerateFiles(args[0], "*.ts", SearchOption.AllDirectories))
                {
                    if (!CheckFile(tsFile, fix))
                        success = false;
                }
            }
            else
            {
                success = CheckFile(args[0], fix);
            }

            Console.WriteLine($"\nTotal errors: {ErrorCount}");
            return success ? 0 : 1;
        }
    }
}
