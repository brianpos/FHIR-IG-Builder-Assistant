﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var parserJson = new FhirJsonParser();
            var parserXml = new FhirXmlParser();
            var serializerJson = new FhirJsonSerializer(new SerializerSettings() { Pretty = true });
            var serializerXml = new FhirXmlSerializer(new SerializerSettings() { Pretty = true });
            var testFilenames = Directory.EnumerateFiles(ResourcesDirectory, "*.*", SearchOption.AllDirectories);

            ImplementationGuide ig = null;
            List<Resource> resources = new List<Resource>();

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

                        if (file.EndsWith(@"\ig.xml") && item is ImplementationGuide)
                        {
                            ig = item as ImplementationGuide;
                        }
                        else
                        {
                            resources.Add(item);
                        }

                        // --------------------------------------
                        // Cleanup the resources
                        // --------------------------------------

                        IConformanceResource conf = item as IConformanceResource;
                        if (conf != null)
                        {
                            // Cleanup the canonical URI
                            // e.g. http://fhir.telstrahealth.com.au/th-ncsr/StructureDefinition/ncsr-patient
                            string generateCanonical = $"http://fhir.telstrahealth.com.au/th-ncsr/{item.ResourceType}/{item.Id}";
                            conf.Url = generateCanonical;

                            //canonicalToSP.Add(item.Url, item);
                            //canonicalToLocalSP.Add(item.Url, item);
                        }

                        if (item is StructureDefinition sd)
                        {
                            // Cleanup StructureDefinition opening element that forge removes
                            sd.Snapshot = null;
                            if (sd.Type != sd.Differential.Element[0].Path)
                            {
                                sd.Differential.Element.Insert(0, new ElementDefinition() { ElementId = sd.Type, Path = sd.Type });
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
                if (!ig.Package.Any(p => p.Resource.Any(r => (r.Source as ResourceReference).Reference == key)))
                {
                    // This resource needs to be included
                    Console.WriteLine($"Added {key} to IG");
                    ig.Package.First().Resource.Add(new ImplementationGuide.ResourceComponent() { Example = false, Source = new ResourceReference(key) });
                }
            }

            string newIGContent = OutputResource(serializerXml, ig);

            if (newIGContent != oldIGContent)
            {
                System.IO.File.WriteAllText(ResourcesDirectory + "/ig.xml", newIGContent);
                Console.WriteLine($"Updated ig.xml");
            }

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
            xw.Formatting = Formatting.Indented;
            xw.IndentChar = '\t';
            xw.Indentation = 1;
            doc.WriteTo(xw);
            return sr.ToString();
        }
    }
}
