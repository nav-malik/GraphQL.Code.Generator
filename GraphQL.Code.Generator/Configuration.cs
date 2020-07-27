using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GraphQL.Code.Generator
{
    public static class Configuration
    {
        public enum Elements
        {
            None = 0,
            //Schema = 1,            
            Query = 2,
            Types = 4,
            //Mutation = 8,
            Repositoy = 16
        }
        public static string InputDllNameAndPath = string.Empty;
        public static Elements ElementsToGenerate;

        /// <summary>
        /// Add words in this fields as key and provide the plural word for each word. Words in tihs dictionay key will be excluded 
        /// English Pluralization and instead these words will be replaced with its pluralized words provided as value. Otherwise 
        /// standard English pluralization will apply where pluralization needed.
        /// <para>The Key and Value strings are case sensitive.</para>
        /// </summary>
        public static IDictionary<string, string> PluralizationFilter = null;

        /// <summary>
        /// If Domain classes contains 'Views' (this is particularly important in if you're generating from Entity Framework classes) 
        /// the provide a Regex which filter classes which are views.
        /// <para>For 'View' classes there'll be only List type queries.</para>
        /// </summary>
        /// 
        public static Regex ViewNameFilter = null;
        public static bool MakeAllFieldsOfViewNullable = true;
        public static class TypeClasses
        {
            /// <summary>
            /// Enter all the private members type (full type with namesapce e.g. GraphQL.Types.ObjectType) then name of the member. 
            /// Particularly use this to add private members for DataLoader type and Repository Type.
            /// If this variable will be empty string then variable name for DataLoader type (IDataLoaderContextAccessor) will be 'accessor'
            /// and variable name for Repository type will be 'repository'.
            /// </summary>
            public static string PrivateMembers = string.Empty;

            public static string DataLoaderPrivateMember = "GraphQL.DataLoader.IDataLoaderContextAccessor accessor";

            public static string RepositoryPrivateMember = string.Empty;

            public static string DataLoaderConstructorParameter = "GraphQL.DataLoader.IDataLoaderContextAccessor accessor";

            public static string RepositoryConstructorParameter = string.Empty;

            public static string ClassSuffix = "Type";
            public static string FileExtension = ".cs";
            public static string Outputpath = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))
                + "/GraphQLTypes/";

            public static string TypeClassesNamespace = string.Empty;

            public static Regex EntityClassesNamespacesInclude = null;
            public static Regex EntityClassesNamespacesExclude = null;
            public static Regex EntityClassNamesInclude = null;
            public static Regex EntityClassNamesExclude = null;

            /// <summary>
            /// Add comma separated full namespace name.
            /// <code>"My.Namespace.Abc, My.Namespace.Xyz"</code>
            /// <para>This will be translated as:</para>
            /// <code>using My.Namespace.Abc;</code>
            /// <code>using My.Namespace.Xyz;</code>
            /// </summary>
            public static string AdditionalNamespaces = string.Empty;
            /// <summary>
            /// Use this filed to add some code into the constructor of the Type classes. This can be handy when you want to use the
            /// benefit of partial class. This field can be use to register extra Fields of GraphQL Type. The extra Fields can be added 
            /// to a method of Partial class and that method can be called with this field.
            /// <para> e.g. if I've a method InitializePartial and in that method I want to register extra GraphQL Fields, then I'll add that
            /// method call in this field as a string like, </para>
            /// <para>Configuration.TypeClasses.AdditionalCodeToBeAddedInConstructor = "InitializePartial();" </para>
            /// <para>Multiple statements can be added with this field separating by semicolon (;) like, </para>
            /// <para>Configuration.TypeClasses.AdditionalCodeToBeAddedInConstructor = "InitializePartial(); CallAnotherMethod();"; </para>
            /// </summary>
            public static string AdditionalCodeToBeAddedInConstructor = string.Empty;


        }

        public static class RepositoryClass
        {
            public static string RepositoryClassesNamespace = string.Empty;

            public static string DBContextPrivateMember = string.Empty;
            public static string DBContextConstructorParameter = string.Empty;

            public static string RepositoryClassName = string.Empty;

            public static string FileExtension = ".cs";
            public static string Outputpath = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))
                + "/GraphQLTypes/";

            /// <summary>
            /// Add comma separated full namespace name.
            /// <code>"My.Namespace.Abc, My.Namespace.Xyz"</code>
            /// <para>This will be translated as:</para>
            /// <code>using My.Namespace.Abc;</code>
            /// <code>using My.Namespace.Xyz;</code>
            /// </summary>
            public static string AdditionalNamespaces = string.Empty;
            /// <summary>
            /// Add IEnumerable of string with method names which Generator shouldn't generate.
            /// <para>This can be use after initial code generation as some methods need to be changed for their implementation.</para>
            /// <para>If any generated method need to be change, then move it to other part of the Partial Class as at Code regeneration
            /// those custom implementation will be overriden.</para>
            /// </summary>
            public static IEnumerable<string> MethodExcludeFilter;
            public static bool IsMethodExcludeFilterApplyToInterface = false;
        }

        public static class QueryClass
        {
            public static string RepositoryPrivateMember = string.Empty;

            public static string RepositoryConstructorParameter = string.Empty;

            public static string QueryClassNamespace = string.Empty;

            public static string QueryClassName = string.Empty;

            public static string FileExtension = ".cs";
            public static string Outputpath = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))
                + "/GraphQLTypes/";

            /// <summary>
            /// Add comma separated full namespace name.
            /// <code>"My.Namespace.Abc, My.Namespace.Xyz"</code>
            /// <para>This will be translated as:</para>
            /// <code>using My.Namespace.Abc;</code>
            /// <code>using My.Namespace.Xyz;</code>
            /// </summary>
            public static string AdditionalNamespaces = string.Empty;
        }
    }
}
