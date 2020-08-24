using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FHIR_IG_Builder_Assistant
{
    public class UpdatePublishBox
    {
        public UpdatePublishBox()
        {
        }

        public void PrepareRelease(string directory)
        {
            var js = new JsonSerializer();
            js.Formatting = Newtonsoft.Json.Formatting.Indented;

            // parse the ig.json
            string igJsonText = System.IO.File.ReadAllText(directory + "/ig.json");
            var igJson = js.Deserialize(new JsonTextReader(new StringReader(igJsonText)));
            Newtonsoft.Json.Linq.JToken t = igJson as Newtonsoft.Json.Linq.JToken;
            var igVersionToken = t["fixed-business-version"] as JValue;
            string igBusinessVersion = igVersionToken.Value.ToString();
            if (string.IsNullOrEmpty(igBusinessVersion))
            {
                Console.Error.WriteLine("No fixed-business-version defined in the ig.json file");
                return;
            }

            // parse the package-list.json file in the folder
            string packageListJsonText = System.IO.File.ReadAllText(directory + "/package-list.json");
            var packageListJson = js.Deserialize(new JsonTextReader(new StringReader(packageListJsonText)));
            t = packageListJson as Newtonsoft.Json.Linq.JToken;
            var igVersionList = t["list"] as JArray;
            string currentFolder = null;
            string currentVersion = null;
            foreach (JObject igVersion in igVersionList)
            {
                string version = igVersion.Value<JToken>("version").ToString();
                string status = igVersion.Value<JToken>("status").ToString();
                string current = igVersion.Value<JToken>("current")?.ToString();
                if (Directory.Exists($"{directory}/{version}") && current == "True" && status != "ci-build")
                {
                    // this is the folder we need to process as the current version
                    ProcessFolder($"{directory}/{version}", "This is the current published version in its permanent home. <a href=\"..\\history.html\">Directory of published versions</a>", true);
                    currentFolder = $"{directory}/{version}";
                    currentVersion = version;

                    if (Directory.Exists($"{directory}/root"))
                    {
                        // this is the folder we need to process as the current version
                        ProcessFolder($"{directory}/root", $"This is the current published version {version}. <a href=\"history.html\">Directory of published versions</a>", true);
                    }
                }
                if (Directory.Exists($"{directory}/{version}") && current != "True")
                {
                    // this is the folder we need to process as the current version
                    ProcessFolder($"{directory}/{version}", $"This version is superseded by <a href=\"..\\{currentVersion ?? igBusinessVersion}\\index.html\">{currentVersion ?? igBusinessVersion}</a>. <a href=\"..\\history.html\">Directory of published versions</a>", false);
                }
            }
            if (!string.IsNullOrEmpty(currentFolder))
                ProcessFolder($"{directory}/output", $"This is the continuous integration build, it is not an authorized publication, and may be broken or incomplete at times. Refer to the <a href=\"..\\history.html\">Directory of published versions</a> for stable versions, or <a href=\"..\\{currentVersion}\\index.html\">{currentVersion}</a> for the current version", true);
            else
                ProcessFolder($"{directory}/output", "This is the continuous integration build, it is not an authorized publication, and may be broken or incomplete at times. Refer to the <a href=\"..\\history.html\">Directory of published versions</a> for stable versions", true);
        }

        public void ProcessFolder(string directory, string replaceText, bool? current)
        {
            foreach (var filename in System.IO.Directory.EnumerateFiles(directory, "*.html", System.IO.SearchOption.AllDirectories))
            {
                if (filename.Contains("\\qa"))
                    continue;
                Console.WriteLine(filename);
                if (!UpdateHtml(filename, "<!--ReleaseHeader-->", "<!--EndReleaseHeader-->", replaceText, current))
                    if (!UpdateHtml(filename, "<!-- ReleaseHeader -->", "<!-- EndReleaseHeader -->", replaceText, current))
                        Console.Error.WriteLine($"Publish Box template not in {filename}");
            }
        }
        private static string oldPublishBoxContent;
        private static bool UpdateHtml(string filename, string publishBoxStart, string publishBoxEnd, string replaceWithContent, bool? current)
        {
            string content = System.IO.File.ReadAllText(filename);
            int startPos = content.IndexOf(publishBoxStart);
            if (startPos == -1)
            {
                return false;
            }
            startPos += publishBoxStart.Length;
            int endPos = content.IndexOf(publishBoxEnd, startPos);
            string newContent = content.Substring(0, startPos);
            if (!string.IsNullOrEmpty(replaceWithContent))
            {
                if (!replaceWithContent.Contains("publish-box"))
                {
                    if (current == true)
                        newContent += "<p id=\"publish-box\">";
                    else
                        newContent += "<p id=\"publish-box-past\">";
                }
                newContent += replaceWithContent;
                if (!replaceWithContent.Contains("publish-box"))
                    newContent += "</p>";
            }
            newContent += content.Substring(endPos);
            if (oldPublishBoxContent == null)
            {
                oldPublishBoxContent = content.Substring(startPos, endPos - startPos);
                Console.WriteLine("PublishBox old text:");
                Console.WriteLine("====================");
                Console.WriteLine(oldPublishBoxContent);
                Console.WriteLine();
                System.IO.File.WriteAllText(filename, newContent);
            }
            else
            {
                //if (oldPublishBoxContent != content.Substring(startPos, endPos - startPos))
                //{
                //    Console.Error.WriteLine($"Publish Box content different");
                //}
                //else
                //{
                    System.IO.File.WriteAllText(filename, newContent);
                //}
            }
            return true;
        }
    }
}
