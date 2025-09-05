using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

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
            Mutation = 8,
            Repositoy = 16,
            MutationInputTypes = 32,
            MutationRepository = 64,
            StoredProcedureAsMutation = 128,
        }
        public enum ORMTypes
        {
            None = 0,
            EF6 = 1,
            EFCore = 2
        }
        public static string InputDllNameAndPath = string.Empty;
        public static Elements ElementsToGenerate;

        /// <summary>
        /// If this field is true then generator will find PrimaryKey and ForiegnKey fields by using KeyAttribute and ForiengKeyAttribute 
        /// respectively.
        /// </summary>
        public static bool UseDataAnnotationsToFindKeys = false;
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
        public static ORMTypes ORMType = ORMTypes.EF6;

        public static string LogsOutputpath = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))
                + "/GraphQLTypes/";

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

            public static bool ConvertByteTypeToStringGraphType = true;
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

        public static class MutationInputTypeClasses
        {           
            public static string ClassSuffix = "InputType";
            public static string NameSuffix = "Input";
            public static string FileExtension = ".cs";
            public static string Outputpath = Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))
                + "/GraphQLTypes/";

            public static string InputTypeClassesNamespace = string.Empty;

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

            /*
            ///<summary>
            ///If true then all fields in the input type for muatations are nullable, 
            ///by default it will depend on underlying Entity classs, apart of single Key field all required 
            /// </summary>
            public static bool MakeAllFieldsNullable = false;

            ///<summary>
            /// If true then it will create separate input type with all fields nullable.
            /// This will be helpful in update mutation to update only few fields and keep original input type coupled with Entity.
            /// </summary>
            public static bool CreateSeparateInputTypeWithAllNullableFields = false;
            ///<summary>
            ///This will use after class and before ClassPostfix
            /// </summary>
            public static string NullableTypeMidfix = "Nullable";
            */

            public static string TypeClassSuffix = "Type";
        }

        public static class MutationRepositoryClass
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

        public static class MutationClass
        {
            public static string RepositoryPrivateMember = string.Empty;

            public static string RepositoryConstructorParameter = string.Empty;

            public static string MutationClassNamespace = string.Empty;

            public static string MutationClassName = string.Empty;

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

        public static class StoredProcedureAsMutation
        {
            public static string InputDllNameAndPath = string.Empty;
            public static bool UseSamePathAsEntities = false;

            public static bool IsDbContextBaseTypeNullAllowed = false;
            public static Regex DdContextBaseClassInclude = new Regex(pattern: @"^DbContext.*$", options: RegexOptions.IgnoreCase);
            public static Regex DdContextBaseClassExclude = null;
            public static Regex DdContextClassNameInclude = null;
            public static Regex DdContextClassNameExclude = null;

            public static Regex MethodInclude = null;
            public static Regex MethodExclude = null;

            public static bool IsAnyMethodReturnTypeAllowed = false;
            public static bool IsMethodReturnTypeBaseTypeNullAllowed = false;
            public static Regex MethodReturnTypeBaseClassInclude = new Regex(pattern: @"^valuetype.*$", options: RegexOptions.IgnoreCase);

            public static bool IgnoreMethodsWithLastOutParam = true;
            public static Regex LastOutParameterNameExclude = new Regex(pattern: @"^procResult.*$", options: RegexOptions.IgnoreCase);
        }
    }
}
