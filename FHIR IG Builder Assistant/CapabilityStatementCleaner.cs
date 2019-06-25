using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace FHIR_IG_Builder_Assistant
{
    public class CapabilityStatementCleaner
    {
        static readonly string IgResourcesDirectory = @"C:\git\VhDir\source\resources";

        DirectorySource source = new DirectorySource(IgResourcesDirectory);

        public void UpdateVhDirConformanceStatementWithSearchParameters()
        {
            var coreSearchParamFiles = System.IO.Directory.EnumerateFiles(@"C:\Users\BPostlethwaite\.fhir\packages\hl7.fhir.core#4.0.0\package", "searchparameter-*.json", System.IO.SearchOption.AllDirectories);
            // Indexing, CanonicalURI
            Dictionary<string, SearchParameter> canonicalToSP = new Dictionary<string, SearchParameter>();
            var parser = new Hl7.Fhir.Serialization.FhirJsonParser();
            var parserXml = new Hl7.Fhir.Serialization.FhirXmlParser();
            var serializer = new Hl7.Fhir.Serialization.FhirJsonSerializer(new Hl7.Fhir.Serialization.SerializerSettings() { Pretty = true });
            var serializerXml = new Hl7.Fhir.Serialization.FhirXmlSerializer(new Hl7.Fhir.Serialization.SerializerSettings() { Pretty = true });
            foreach (var fileCore in coreSearchParamFiles)
            {
                using (var stream = System.IO.File.Open(fileCore, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    System.IO.StreamReader sr = new System.IO.StreamReader(stream);
                    SearchParameter item = parser.Parse<SearchParameter>(sr.ReadToEnd());
                    if (item.Url == "http://hl7.org/fhir/SearchParameter/example-reference")
                        continue;
                    canonicalToSP.Add(item.Url, item);
                }
            }
            System.Diagnostics.Trace.WriteLine("");

            // Read the Implementation IG resource
            string oldIGXml = System.IO.File.ReadAllText($"{IgResourcesDirectory}/capabilitystatement-server.xml");
            CapabilityStatement capStmt = parserXml.Parse<CapabilityStatement>(oldIGXml);
            oldIGXml = OutputResource(serializerXml, capStmt);
            Dictionary<string, SearchParameter> canonicalToLocalSP = new Dictionary<string, SearchParameter>();

            var testFilenames = System.IO.Directory.EnumerateFiles(IgResourcesDirectory, "searchparameter-*.json", System.IO.SearchOption.AllDirectories);
            foreach (var file in testFilenames)
            {
                using (var stream = System.IO.File.Open(file, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    System.IO.StreamReader sr = new System.IO.StreamReader(stream);
                    string oldJson = sr.ReadToEnd();
                    // Close the stream so we can re-write it
                    stream.Close();
                    SearchParameter item = parser.Parse<SearchParameter>(oldJson);
                    canonicalToSP.Add(item.Url, item);
                    canonicalToLocalSP.Add(item.Url, item);
                }
            }

            foreach (CapabilityStatement.SearchParamComponent item in capStmt.Rest.SelectMany(rest => rest.Resource).SelectMany(rt => rt.SearchParam))
            {
                if (canonicalToSP.ContainsKey(item.Definition))
                {
                    var searchDefinition = canonicalToSP[item.Definition];
                    if (searchDefinition.Code != item.Name)
                    {
                        System.Diagnostics.Trace.WriteLine($"Search Parameter definition {item.Definition} needs to be updated - from {item.Name} to {searchDefinition.Code}");
                        item.Name = searchDefinition.Code;
                    }
                    if (searchDefinition.Type != item.Type)
                    {
                        System.Diagnostics.Trace.WriteLine($"Search Parameter definition {item.Definition} needs to be updated - from {item.Type} to {searchDefinition.Type}");
                        item.Type = searchDefinition.Type;
                    }
                    if (canonicalToLocalSP.ContainsKey(item.Definition))
                    {
                        canonicalToLocalSP.Remove(item.Definition);
                    }
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"Search Parameter definition {item.Definition} was not found in the generation");
                }
            }

            // Now check if there are any local search parameters that are not in the Capability Statement
            foreach (var item in canonicalToLocalSP.Values)
            {
                System.Diagnostics.Trace.WriteLine($"Search Parameter definition {item.Url} was not found in the CapabilityStatement");
                foreach (var resType in capStmt.Rest.SelectMany(rest => rest.Resource).Where(r => item.Base.Contains(r.Type)))
                {
                    resType.SearchParam.Add(new CapabilityStatement.SearchParamComponent()
                    {
                        Name = item.Code,
                        Definition = item.Url,
                        Type = item.Type,
                        Documentation = item.Description
                    });
                }
            }

            string newIGXml = OutputResource(serializerXml, capStmt);
            if (newIGXml != oldIGXml)
                System.IO.File.WriteAllText($"{IgResourcesDirectory}/capabilitystatement-server.xml", newIGXml);
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
