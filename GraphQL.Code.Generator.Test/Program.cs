using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GraphQL.Code.Generator.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            //GetPath();
            //return;
            Configuration.ElementsToGenerate = Configuration.Elements.Types | Configuration.Elements.Repositoy | Configuration.Elements.Query;

            Configuration.TypeClasses.RepositoryConstructorParameter
                = "IBCDM.Repository.GlobalApprovers.IGlobalApproversReposiroty globalApproverRepository";
            Configuration.TypeClasses.RepositoryPrivateMember
                = "IBCDM.Repository.GlobalApprovers.IGlobalApproversReposiroty _globalApproverRepository";

            Configuration.TypeClasses.TypeClassesNamespace = "IBCDM.Graph.Types.GlobalApprovers";
            Configuration.TypeClasses.EntityClassesNamespacesInclude
                = new Regex(@"^NHLStats.Core.Models$|^HobbyDataLayer.Models$|^PizzaOrder.Data.Entities$", RegexOptions.IgnoreCase);
            Configuration.TypeClasses.EntityClassNamesExclude =
                new Regex(@"^Rusp.*$|^DatabaseInitializer$|^DBManager$|^DataSeeder$|.*MainClass.*|.*SubClass.*", RegexOptions.IgnoreCase);

            Configuration.TypeClasses.AdditionalNamespaces = "IBCDM.Data.Models.GlobalApprovers, IBCDM.DomainTypes.Models.GlobalApprovers";

            string pathCore = @"H:\Udemy\soft\Code\ASPNetCoreGraphQL-master\src\backend\NHLStats.Core\obj\Debug\netcoreapp2.2\NHLStats.Core.dll";
            string pathFramework = @"H:\Udemy\soft\Code\GraphQLHobbyAPI-master\HobbyDataLayer\bin\Debug\HobbyDataLayer.dll";
            string pathCore31 = @"H:\Udemy\soft\Code\GraphQL-Demo-master\Coding\PizzaOrder\PizzaOrder.Data\bin\Debug\netcoreapp3.1\PizzaOrder.Data.dll";

            Configuration.InputDllNameAndPath = pathCore;
            //Configuration.TypeClasses.EntityClassNamesInclude
            //    = new Regex(@"^Hobby$|^Person$", RegexOptions.IgnoreCase);

            Dictionary<string, string> pluralizationFilter = new Dictionary<string, string>();
            pluralizationFilter.Add("Person", "Persons");
            Configuration.PluralizationFilter = pluralizationFilter;

            Configuration.TypeClasses.AdditionalCodeToBeAddedInConstructor = "InitializePartial();";

            Configuration.RepositoryClass.DBContextPrivateMember
                = "IBCDM.Data.Model.GlobalApprovers.IGlobalApproversDBContext _context";
            Configuration.RepositoryClass.DBContextConstructorParameter
                = "IBCDM.Data.Model.GlobalApprovers.IGlobalApproversDBContext context";
            Configuration.RepositoryClass.RepositoryClassName = "GlobalApproversRepository";
            Configuration.RepositoryClass.RepositoryClassesNamespace = "IBCDM.Repository.GlobalApprovers";
            Configuration.RepositoryClass.AdditionalNamespaces = "IBCDM.Data.Model.GlobalApprovers";
            //Configuration.RepositoryClass.MethodExcludeFilter = new List<string> { "GetSkaterStatistics" };
            //Configuration.RepositoryClass.IsMethodExcludeFilterApplyToInterface = true;

            //Configuration.TypeClasses.Outputpath = @"H:\Udemy\soft\Code\GraphQL-Generators\GraphQL.Code.Generator\GraphQL.Code.Generator.Test\bin\GenFiles_Core";
            //Configuration.TypeClasses.Outputpath = @"H:\Udemy\soft\Code\GraphQL-Generators\GraphQL.Code.Generator\GraphQL.Code.Generator.Test\bin\GenFiles_Net";
            
            Configuration.QueryClass.RepositoryConstructorParameter
                = "IBCDM.Repository.GlobalApprovers.IGlobalApproversReposiroty globalApproverRepository";
            Configuration.QueryClass.RepositoryPrivateMember
                = "IBCDM.Repository.GlobalApprovers.IGlobalApproversReposiroty globalApproverRepository";
            Configuration.QueryClass.QueryClassName = "IBCDMQuery";
            Configuration.QueryClass.QueryClassNamespace = "IBCDM.Graph.Queries";
            Configuration.QueryClass.AdditionalNamespaces = "IBCDM.Repository.GlobalApprovers, IBCDM.Graph.Types.GlobalApprovers";
            Configuration.TypeClasses.Outputpath += @"\Types_";
            Configuration.RepositoryClass.Outputpath += @"\Repository_1";
            Configuration.QueryClass.Outputpath += @"\Query_1";
            int fileCount = GraphQL.Code.Generator.GraphQLCodeGenerator.GenerateGraphQLCode();
            Console.WriteLine(fileCount + " GraphQL files generated.");
            Console.ReadLine();
        }

        static void GetPath()
        {
            Assembly SampleAssembly;
            // Instantiate a target object.
            Int32 Integer1 = new Int32();
            Type Type1;
            // Set the Type instance to the target class type.
            Type1 = Integer1.GetType();
            // Instantiate an Assembly class to the assembly housing the Integer type.  
            SampleAssembly = Assembly.GetAssembly(typeof(Configuration));
            // Display the physical location of the assembly containing the manifest.
            Console.WriteLine("Location=" + SampleAssembly.Location);
            Console.ReadLine();
            // The example displays the following output:
            //   Location=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll
        }
    }
}
