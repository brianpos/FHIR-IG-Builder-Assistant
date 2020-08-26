using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FHIR_IG_Builder_Assistant
{

    public class StripDownOpenAPI
    {
        public string _directory;
        public StripDownOpenAPI(string directory)
        {
            _directory = directory;
        }
        public void MakeOpenApiDocumentNotExternal()
        {
            Console.WriteLine("Cleaning the openAPI outputs");
            var files = System.IO.Directory.EnumerateFiles(_directory + "/output", "*.openapi.json", SearchOption.AllDirectories).ToArray();
            foreach (string file in files)
            {
                string igJsonText = System.IO.File.ReadAllText(file);
                var js = new JsonSerializer();
                js.Formatting = Newtonsoft.Json.Formatting.Indented;
                var igJson = js.Deserialize(new JsonTextReader(new StringReader(igJsonText)));
                Newtonsoft.Json.Linq.JToken t = igJson as Newtonsoft.Json.Linq.JToken;

                // remove all of the application/fhir+xml content
                var schemasXml = t.SelectTokens("..application/fhir+xml");
                foreach (var schemaXml in schemasXml.ToArray())
                {
                    schemaXml.Parent.Remove();
                }

                Dictionary<string, JToken> convertedTypes = new Dictionary<string, JToken>();

                var schemas = t.SelectTokens("..schema.$ref");
                foreach (JValue schema in schemas)
                {
                    string value = schema.Value as string;
                    if (!value.StartsWith("#") && value.Contains("#"))
                    {
                        if (!convertedTypes.ContainsKey(value.Replace("https://hl7.org/fhir/STU3/fhir.schema.json", "")))
                        {
                            convertedTypes.Add(value.Replace("https://hl7.org/fhir/STU3/fhir.schema.json", ""), null);
                        }
                        if (!convertedTypes.ContainsKey(value.Replace("https://hl7.org/fhir/R4/fhir.schema.json", "")))
                        {
                            convertedTypes.Add(value.Replace("https://hl7.org/fhir/R4/fhir.schema.json", ""), null);
                        }
                        schema.Value = value.Substring(value.IndexOf("#"));
                    }
                }

                JObject definitions = t["definitions"] as JObject;
                if (definitions != null)
                {
                    definitions.Remove();
                }

                string fhirJsonSchemaText = System.IO.File.ReadAllText(@"C:\temp\fhir.schema.json");
                var fhirJsonSchema = js.Deserialize(new JsonTextReader(new StringReader(fhirJsonSchemaText))) as JToken;
                while (convertedTypes.Where(ct => ct.Value == null).Count() > 0)
                {
                    foreach (var value in convertedTypes.Keys.ToArray())
                    {
                        var search = value.Substring(2).Replace("/", ".");
                        var typeToken = fhirJsonSchema.SelectToken(search).Parent;
                        // Console.WriteLine($"{value}");
                        // Console.WriteLine($"{search}");
                        // Console.WriteLine($"{typeToken}");
                        convertedTypes[value] = typeToken;

                        // Scan the token for nested types
                        if (search == "definitions.ResourceList")
                        {
                            foreach (JValue ct in typeToken.SelectTokens("..$ref").ToArray())
                            {
                                if (ct.Value is string s)
                                {
                                    if (!convertedTypes.ContainsKey(s))
                                    {
                                        System.Diagnostics.Trace.WriteLine($"Removing unused type {s}");
                                        ct.Parent.Parent.Remove();
                                    }
                                }
                            }

                            continue;
                        }
                        foreach (JValue ct in typeToken.SelectTokens("..$ref"))
                        {
                            if (ct.Value is string s)
                            {
                                if (!convertedTypes.ContainsKey(s))
                                {
                                    // System.Diagnostics.Trace.WriteLine($"{s}");
                                    convertedTypes.Add(s, null);
                                }
                            }
                        }
                        
                        // and remove all the extension properties
                        foreach (JToken ct in typeToken.SelectTokens("..properties").Children().ToArray())
                        {
                            // System.Diagnostics.Trace.WriteLine($"{ct.Path.Replace(ct.Parent.Path+".", "")}");
                            if (value == "#/definitions/BackboneElement")
                            {
                                ct.Parent.Parent.Parent.Remove();
                                continue;
                            }
                            if (ct.Path.Replace(ct.Parent.Path + ".", "").StartsWith("_"))
                                ct.Remove();
                            else if (ct.Path.Replace(ct.Parent.Path + ".", "") == "modifierExtension")
                                ct.Remove();
                            else if (ct.Path.Replace(ct.Parent.Path + ".", "") == "extension")
                                ct.Remove();
                            else if (value == "#/definitions/Element")
                            {
                                ct.Remove();
                            }
                        }
                    }
                }
                t.Children().Last().AddAfterSelf(new JProperty("definitions", new JObject(convertedTypes.Values.ToArray())));

                //foreach (var pair in fileResources)
                //{
                //    FileInfo fi = new FileInfo(pair.Key);
                //    string htmlName = $"{pair.Value.ResourceType.GetLiteral()}-{fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length)}.html";
                //    htmlName = htmlName.Replace(pair.Value.ResourceType.GetLiteral().ToLower(), "");
                //    htmlName = htmlName.Replace("--", "-");
                //    string content = $"{{\r\n  \"{pair.Value.ResourceType.GetLiteral()}/{pair.Value.Id}\": {{\r\n" +
                //        $"    \"source\" : \"{fi.Name}\",\r\n" +
                //        $"    \"base\" : \"{htmlName}\"\r\n" +
                //        $"  }}\r\n}}";
                //    var newNode = Newtonsoft.Json.Linq.JObject.Parse(content) as JToken;
                //    igJsonResources.Add(newNode.Children().First());
                //}
                var sb = new StringBuilder();
                js.Serialize(new JsonTextWriter(new StringWriter(sb)), igJson);

                if (sb.ToString() != igJsonText)
                    System.IO.File.WriteAllText(file+".json", sb.ToString());
                // Console.WriteLine(sb.ToString());
            }
        }
    }
}
