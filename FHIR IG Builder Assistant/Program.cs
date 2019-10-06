using System;
using System.Linq;

namespace FHIR_IG_Builder_Assistant
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Processing the Implementation Guide in {Environment.CurrentDirectory}");

            if (args.ToList().Contains("-normalize"))
            {
                var igc = new Stu3ImplementationGuideCleaner(Environment.CurrentDirectory);
                igc.CanonicalizeAllResources();
                return;
            }
            if (args.ToList().Contains("-cleanopenapi"))
            {
                new StripDownOpenAPI(Environment.CurrentDirectory).MakeOpenApiDocumentNotExternal();
                return;
            }

            //var capStmt = new CapabilityStatementCleaner();
            //capStmt.UpdateVhDirConformanceStatementWithSearchParameters();
            Console.WriteLine("Usage:");
            Console.WriteLine(" dotnet FHIR IG Builder Assistant.dll -normalize");
            Console.WriteLine(" dotnet FHIR IG Builder Assistant.dll -cleanopenapi");
        }
    }
}
