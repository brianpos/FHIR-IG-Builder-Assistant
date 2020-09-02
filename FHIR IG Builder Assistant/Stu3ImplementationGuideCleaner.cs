using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace FHIR_IG_Builder_Assistant
{
    public class Stu3ImplementationGuideCleaner
    {
        #region << Constructors and accessors >>
        private string _directory;
        public Stu3ImplementationGuideCleaner(string directory)
        {
            _directory = directory;
        }

        /// <summary>
        /// e.g. IG/resources
        /// </summary>
        private string ResourcesDirectory { get { return Path.Combine(_directory, "resources"); } }
        /// <summary>
        /// e.g. IG/pages
        /// </summary>
        private string PagesFolderDirectory { get { return Path.Combine(_directory, "pages"); } }
        /// <summary>
        /// e.g. IG/pages/_includes
        /// </summary>
        private string PagesIncludesDirectory { get { return Path.Combine(_directory, "pages/_includes"); } }
        private string[] ignoreResourceDirectoryFiles = { "ig-expansion-parameters.json", "ig-new.json", "ig-new.xml", "ig-validation-parameters.json" };
        #endregion


        public void CanonicalizeAllResources()
        {
            Console.WriteLine("Canonicalizing All resources");
            var parserJson = new FhirJsonParser();
            var parserXml = new FhirXmlParser();
            var serializerJson = new FhirJsonSerializer(new SerializerSettings() { Pretty = true });
            var serializerXml = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var testFilenames = Directory.EnumerateFiles(ResourcesDirectory, "*.*", SearchOption.AllDirectories);

            // read the canonical URI for the guide
            string igJsonText = System.IO.File.ReadAllText(_directory + "/ig.json");
            var js = new JsonSerializer();
            js.Formatting = Newtonsoft.Json.Formatting.Indented;
            var igJson = js.Deserialize(new JsonTextReader(new StringReader(igJsonText)));
            Newtonsoft.Json.Linq.JToken t = igJson as Newtonsoft.Json.Linq.JToken;
            var igJsonCanonicalBase = t["canonicalBase"] as JValue;
            string canonicalBase = igJsonCanonicalBase.Value.ToString();
            if (string.IsNullOrEmpty(canonicalBase))
            {
                Console.Error.WriteLine("No canonical Base defined in the ig.json file");
                return;
            }
            string namePrefix = canonicalBase.Substring(canonicalBase.LastIndexOf("/") + 1);
            if (namePrefix.Contains("-"))
                namePrefix = namePrefix.Substring(namePrefix.IndexOf("-") + 1);
            namePrefix = namePrefix.Substring(0, 1).ToUpper() + namePrefix.Substring(1);


            ImplementationGuide ig = parserXml.Parse<ImplementationGuide>(File.ReadAllText(Path.Combine(ResourcesDirectory, "ig.xml")));
            List<Resource> resources = new List<Resource>();
            Dictionary<string, Resource> fileResources = new Dictionary<string, Resource>();

            foreach (var file in testFilenames)
            {
                if (ignoreResourceDirectoryFiles.Any(i => file.EndsWith(i)))
                    continue;
                using (var stream = System.IO.File.Open(file, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    System.IO.StreamReader sr = new System.IO.StreamReader(stream);
                    string oldContent = sr.ReadToEnd();
                    // Close the stream so we can re-write it
                    stream.Close();

                    try
                    {
                        Resource item = null;
                        if (file.EndsWith("json"))
                        {
                            Console.WriteLine($"Processing JSON {file}");
                            item = parserJson.Parse<Resource>(oldContent);
                            oldContent = serializerJson.SerializeToString(item);
                        }
                        else if (file.EndsWith("xml"))
                        {
                            Console.WriteLine($"Processing XML {file}");
                            item = parserXml.Parse<Resource>(oldContent);
                            oldContent = OutputResource(serializerXml, item);
                        }
                        else
                        {
                            // not a FHIR resource in STU3
                            Console.WriteLine($"XXX Not Processing {file}");
                            continue;
                        }

                        if ((file.EndsWith(@"\ig.xml") || file.EndsWith(@"/ig.xml")) && item is ImplementationGuide)
                        {
                            ig = item as ImplementationGuide;
                        }
                        else
                        {
                            resources.Add(item);
                            if (item is IConformanceResource)
                                fileResources.Add(file, item);
                        }

                        // --------------------------------------
                        // Cleanup the resources
                        // --------------------------------------

                        IConformanceResource conf = item as IConformanceResource;
                        if (conf != null)
                        {
                            // Cleanup the canonical URI
                            // e.g. http://fhir.telstrahealth.com.au/th-epd/StructureDefinition/epd-practitioner
                            string generateCanonical = $"{canonicalBase}/{item.ResourceType}/{item.Id}";
                            if (!conf.Url.StartsWith("http://hl7.org.au") && !(item is ImplementationGuide))
                                conf.Url = generateCanonical;

                            Regex r = new Regex("^[A-Z]([A-Za-z0-9_]){0,254}$", RegexOptions.Singleline);
                            if (conf.Name == null || !r.IsMatch(conf.Name))
                            {
                                conf.Name = $"{namePrefix}_{item.Id.Replace("searchparameter-", "_sp_").Replace("-", "_")}";
                                if (!r.IsMatch(conf.Name))
                                {
                                    System.Diagnostics.Trace.WriteLine($"Replaced Name property is still invalid {conf.Name}");
                                }
                            }
                            if (conf.Publisher != ig.Publisher)
                            {
                                conf.Publisher = ig.Publisher;
                            }

                            //canonicalToSP.Add(item.Url, item);
                            //canonicalToLocalSP.Add(item.Url, item);
                        }

                        if (item is StructureDefinition sd)
                        {
                            // remove the snapshot - that get's regenerated anyway
                            sd.Snapshot = null;

                            // Cleanup StructureDefinition opening element that forge removes
                            if (sd.Type != sd.Differential.Element[0].Path)
                            {
                                sd.Differential.Element.Insert(0, new ElementDefinition() { ElementId = sd.Type, Path = sd.Type });
                            }

                            // Check any extension URLs
                            var elemExtensionUrl = sd.Differential.Element.FirstOrDefault(e => e.Path == "Extension.url")?.Fixed as FhirUri;
                            if (elemExtensionUrl != null)
                            {
                                if (elemExtensionUrl.Value != conf.Url)
                                {
                                    Console.WriteLine($"Fixed Extension URL from {elemExtensionUrl.Value} to {conf.Url}");
                                    elemExtensionUrl.Value = conf.Url;
                                }
                            }

                            // Check for existence of the markdown files
                            string prefix = new FileInfo(file).Name;
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-intro.md");
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-summary.md");
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-search.md");
                        }

                        if (item is SearchParameter sp)
                        {
                            // Check for existence of the markdown files
                            string prefix = new FileInfo(file).Name;
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-intro.md");
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-summary.md");
                            CreateMarkdownStubIfMissing($"{PagesIncludesDirectory}/{item.Id}-search.md");
                        }

                        if (item is CodeSystem cs)
                        {
                            if (cs.Title == null)
                                cs.Title = cs.Name;
                            if (cs.Description == null)
                                cs.Description = new Markdown(cs.Name);
                        }

                        if (item is ValueSet vs)
                        {
                            if (vs.Title == null)
                                vs.Title = vs.Name;
                            if (vs.Description == null)
                                vs.Description = new Markdown(vs.Name);
                        }

                        string newContent;
                        if (file.EndsWith("json"))
                        {
                            newContent = serializerJson.SerializeToString(item);
                        }
                        else
                        {
                            newContent = OutputResource(serializerXml, item);
                        }
                        if (newContent != oldContent)
                        {
                            System.IO.File.WriteAllText(file, newContent);
                            Console.WriteLine($"Updated {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception occurred processing {file} {ex.Message}");
                    }
                }
            }

            // Everything has been processed, so lets check the IG.xml for all the resources
            string oldIGContent = OutputResource(serializerXml, ig);

            foreach (var item in resources)
            {
                string key = $"{item.ResourceType}/{item.Id}";
                if (!ig.Definition.Resource.Any(r => r.Reference.Reference == key))
                {
                    // This resource needs to be included
                    Console.WriteLine($"Added {key} to IG");
                    ig.Definition.Resource.Add(new ImplementationGuide.ResourceComponent() { Example = null, Reference = new ResourceReference(key) });
                }
                else
                {
                    // copy the name in
                    var res = ig.Definition.Resource.First(r => r.Reference.Reference == key);
                    if (resources.First(r => $"{r.TypeName}/{r.Id}" == key) is IConformanceResource conf)
                    {
                        res.Name = conf.Name;
                    }
                }
            }

            // Update the sequencing of the resources in the IG.xml (and alphabetical by name within each type)
            var packResource = new List<ImplementationGuide.ResourceComponent>();
            var p = ig.Definition;

            // Code Systems
            packResource.AddRange(FilterResourcesToType(p.Resource, "CodeSystem").OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterResourcesToType(p.Resource, "CodeSystem"));

            // ValueSets
            packResource.AddRange(FilterResourcesToType(p.Resource, "ValueSet").OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterResourcesToType(p.Resource, "ValueSet"));

            // Extension Definitions
            packResource.AddRange(FilterStructureDefinitions(p.Resource, true, resources).OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterStructureDefinitions(p.Resource, true, resources));

            // Profile Definitions
            packResource.AddRange(FilterStructureDefinitions(p.Resource, false, resources).OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterStructureDefinitions(p.Resource, false, resources));

            // Operation Definitions
            packResource.AddRange(FilterResourcesToType(p.Resource, "OperationDefinition").OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterResourcesToType(p.Resource, "OperationDefinition"));

            // Search Parameters
            packResource.AddRange(FilterResourcesToType(p.Resource, "SearchParameter").OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterResourcesToType(p.Resource, "SearchParameter"));

            // Capability Statements
            packResource.AddRange(FilterResourcesToType(p.Resource, "CapabilityStatement").OrderBy(o => o.Name));
            RemoveRange(p.Resource, FilterResourcesToType(p.Resource, "CapabilityStatement"));

            // anything else
            packResource.AddRange(p.Resource);
            p.Resource = packResource;

            // Now re-pack the manifest section
            //if (ig.Manifest == null)
            //    ig.Manifest = new ImplementationGuide.ManifestComponent();
            //ig.Manifest.Resource.Clear();
            //foreach (var pair in fileResources)
            //{
            //    ig.Manifest.Resource.Add(new ImplementationGuide.ManifestResourceComponent()
            //    {
            //        Reference = new ResourceReference($"{pair.Value.ResourceType.GetLiteral()}/{pair.Value.Id}"),
            //        RelativePath = $"{pair.Value.ResourceType.GetLiteral()}-{pair.Value.Id}.html"
            //    });
            //}

            string newIGContent = OutputResource(serializerXml, ig);

            if (newIGContent != oldIGContent)
            {
                System.IO.File.WriteAllText(ResourcesDirectory + "/ig.xml", newIGContent);
                Console.WriteLine($"Updated ig.xml");
            }

            // And re-process the IG.json file to generate the resources section
            var igJsonResources = t["resources"] as JContainer;
            igJsonResources.RemoveAll();
            foreach (var pair in fileResources)
            {
                FileInfo fi = new FileInfo(pair.Key);
                string htmlName = $"{pair.Value.ResourceType.GetLiteral()}-{fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length)}.html";
                htmlName = htmlName.Replace(pair.Value.ResourceType.GetLiteral().ToLower(), "");
                htmlName = htmlName.Replace("--", "-");
                htmlName = htmlName.Replace($"{pair.Value.ResourceType.GetLiteral()}-{pair.Value.ResourceType.GetLiteral()}-", $"{pair.Value.ResourceType.GetLiteral()}-");
                string content = $"{{\r\n  \"{pair.Value.ResourceType.GetLiteral()}/{pair.Value.Id}\": {{\r\n" +
                    $"    \"source\" : \"{fi.Name}\",\r\n" +
                    $"    \"base\" : \"{htmlName}\"\r\n" +
                    $"  }}\r\n}}";
                var newNode = Newtonsoft.Json.Linq.JObject.Parse(content) as JToken;
                igJsonResources.Add(newNode.Children().First());
            }
            var sb = new StringBuilder();
            js.Serialize(new JsonTextWriter(new StringWriter(sb)), igJson);
            System.IO.File.WriteAllText(_directory + "/ig.json", sb.ToString());
        }

        private void RemoveRange(List<ImplementationGuide.ResourceComponent> resource, IEnumerable<ImplementationGuide.ResourceComponent> removeThese)
        {
            foreach (var item in removeThese.ToArray())
            {
                resource.Remove(item);
            }
        }

        private static IEnumerable<ImplementationGuide.ResourceComponent> FilterStructureDefinitions(List<ImplementationGuide.ResourceComponent> list, bool extensions, List<Resource> resources)
        {
            var results = list.Where(r => r.Example != null && r.Reference.Reference.StartsWith("StructureDefinition"));
            
            // now filter this list further checking if the referenced SD is an extension or not
            var filteredExtensions = results.Where(r => resources.Any(item => r.Reference.Reference == "StructureDefinition/" + item.Id && (item as StructureDefinition)?.Kind == StructureDefinition.StructureDefinitionKind.ComplexType));
            if (extensions)
                return filteredExtensions;
            return results.Except(filteredExtensions);
        }

        private static IEnumerable<ImplementationGuide.ResourceComponent> FilterResourcesToType(List<ImplementationGuide.ResourceComponent> list, string resourceName)
        {
            return list.Where(r => r.Example != null && r.Reference.Reference.StartsWith(resourceName));
        }

        private void CreateMarkdownStubIfMissing(string filename)
        {
            if (!File.Exists(filename))
            {
                File.WriteAllText(filename, "");
            }
        }

        private string OutputResource(FhirXmlSerializer serializer, Resource item)
        {
            string newJson = serializer.SerializeToString(item);
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(newJson);
            var sr = new System.IO.StringWriter();
            var xw = new XmlTextWriter(sr);
            xw.Formatting = System.Xml.Formatting.Indented;
            xw.IndentChar = '\t';
            xw.Indentation = 1;
            doc.WriteTo(xw);
            return sr.ToString();
        }
    }
}
