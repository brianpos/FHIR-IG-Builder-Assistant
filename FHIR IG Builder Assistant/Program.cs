using System;
using System.Linq;

namespace FHIR_IG_Builder_Assistant
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine($"Processing the Implementation Guide in {Environment.CurrentDirectory}");

            if (args.ToList().Contains("-normalize"))
            {
                var igc = new Stu3ImplementationGuideCleaner(Environment.CurrentDirectory);
                igc.CanonicalizeAllResources();
                return 0;
            }
            if (args.ToList().Contains("-cleanopenapi"))
            {
                new StripDownOpenAPI(Environment.CurrentDirectory).MakeOpenApiDocumentNotExternal();
                return 0;
            }

            if (args.ToList().Contains("-updatepublishbox"))
            {
                new UpdatePublishBox().ProcessFolder(Environment.CurrentDirectory, args[1], null);
                return 0;
            }

            if (args.ToList().Contains("-prepare-release"))
            {
                new UpdatePublishBox().PrepareRelease(Environment.CurrentDirectory);
                return 0;
            }


            //var capStmt = new CapabilityStatementCleaner();
            //capStmt.UpdateVhDirConformanceStatementWithSearchParameters();
            Console.WriteLine("Usage:");
            Console.WriteLine(" dotnet FHIR_IG_Builder_Assistant.dll -normalize");
            Console.WriteLine(" dotnet FHIR_IG_Builder_Assistant.dll -cleanopenapi");
            Console.WriteLine(" dotnet FHIR_IG_Builder_Assistant.dll -updatepublishbox \"update with this text\"");
            Console.WriteLine(" dotnet FHIR_IG_Builder_Assistant.dll -prepare-release");
            return -1;
        }
    }
}
