using System;

namespace FHIR_IG_Builder_Assistant
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Processing the Implementation Guide in {Environment.CurrentDirectory}");

            var igc = new Stu3ImplementationGuideCleaner(Environment.CurrentDirectory);
            igc.CanonicalizeAllResources();
            //var capStmt = new CapabilityStatementCleaner();
            //capStmt.UpdateVhDirConformanceStatementWithSearchParameters();
        }
    }
}
