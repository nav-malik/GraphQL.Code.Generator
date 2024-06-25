using System;
using System.Reflection;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
//using System.Data.Entity.Infrastructure.Pluralization;
//using Pluralize.NET.Core;
using Pluralize.NET;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace GraphQL.Code.Generator
{
    public static class GraphQLCodeGenerator
    {
        private static Type GetUnderlyingType(this Type source)
        {
            if (source.IsGenericType && source.FullName.ToLower().Contains("nullable"))
                return Nullable.GetUnderlyingType(source);
            else
                return source;
        }
        private static class Utility
        {
            public static List<int[]> listOfArray = new List<int[]>();
            public static string getCamelCaseString(string input)
            {
                string camelString = string.Empty;
                if (!string.IsNullOrEmpty(input))
                    camelString = char.ToLower(input[0]) + input.Substring(1);
                return camelString;
            }

            public static string getTitleCaseString(string input)
            {
                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                return textInfo.ToTitleCase(input);
            }
            public static int[] getArrayOfIndicesOfList(int elementCount)
            {
                int[] arr = new int[elementCount];
                for (int i = 0; i < elementCount; i++)
                {
                    arr[i] = i;
                }
                return arr;
            }

            /* arr[] ---> Input Array 
	        data[] ---> Temporary array to 
				        store current combination 
	        start & end ---> Staring and Ending 
					        indexes in arr[] 
	        index ---> Current index in data[] 
	        r ---> Size of a combination 
			to be printed */
            public static void combinationUtil(int[] arr, ref int[] data,
                                        int start, int end,
                                        int index, int r)
            {
                // Current combination is 
                // ready to be printed, 
                // print it 
                if (index == r)
                {
                    //for (int j = 0; j < r; j++)
                    //    Console.Write(data[j] + " ");
                    //Console.WriteLine("");
                    listOfArray.Add((int[])data.Clone());
                    return;
                }

                // replace index with all 
                // possible elements. The 
                // condition "end-i+1 >= 
                // r-index" makes sure that 
                // including one element 
                // at index will make a 
                // combination with remaining 
                // elements at remaining positions 
                for (int i = start; i <= end &&
                        end - i + 1 >= r - index; i++)
                {
                    data[index] = arr[i];
                    combinationUtil(arr, ref data, i + 1,
                                    end, index + 1, r);
                }
            }

            // The main function that prints 
            // all combinations of size r 
            // in arr[] of size n. This 
            // function mainly uses combinationUtil() 
            public static int[] getCombination(int[] arr,
                                        int n, int r)
            {
                // A temporary array to store 
                // all combination one by one 
                int[] data = new int[r];

                // Print all combination 
                // using temprary array 'data[]' 
                combinationUtil(arr, ref data, 0,
                                n - 1, 0, r);

                return data;
            }
        }
        private class LogElement
        {
            public bool isException = false;
            public Exception exception = null;
            public string message = "File successfully created.";

        }
        private class QueryParameterMapping
        {
            /// <summary>
            /// In GraphQL Query class this parameter name will be in Camel case and Repository class it will be in Title case.
            /// </summary>
            public string ParameterName;
            /// <summary>
            /// Enter GraphQL Scalar Type name like, "IntGraphType".
            /// </summary>
            public string GraphQLParameterType;
            /// <summary>
            /// Enter corresponding C# type like GraphQLParameterType "IntGraphType" will be equal to "int".
            /// </summary>
            public Type CSharpParameterType;

            /// <summary>
            /// Default value is true, so can be left unset if param is nullable.
            /// </summary>
            public bool IsNullable = true;
        }

        private class LoaderRepositoryMapping
        {
            /// <summary>
            /// If will be the same as Entity Class name of the property e.g. Hobby in GetHobbiesByPersonIdAsync method.
            /// </summary>
            public string ReturnTypeBaseEntityName;
            public string ReturnTypeBaseEntityFullName;
            /// <summary>
            /// This field will be use to get values from context object (context can be DB Context or any other object values will 
            /// be stored). Ideally, it'll be Pluralize form of RentrunTypeEntityName but can be changed.
            /// <para>For example, context.Hobbies in GetHobbiesByPersonIdAsync method.</para>
            /// </summary>
            public string ContextProtpertyName;
            /// <summary>
            /// If IsByParent is false then it ById method and it will be dictionary type method not list type in ToDictionary use the 
            /// same id field from WhereClauseIdFieldName.
            /// <para>If GraphQL return type for the field is ListGraphType then this field is true.</para>
            /// </summary>
            public bool IsByParent;
            public string IdsParamerterName;
            public string WhereClauseIdFieldName;
            public string PropertyType;
        }

        private class QueryRepositoryMethodMapping
        {
            /// <summary>
            /// If will be the same as Entity Class name of the property e.g. Hobby in GetHobbiesByPersonIdAsync method.
            /// </summary>
            public string ReturnTypeBaseEntityName;
            public string ReturnTypeBaseEntityFullName;
            /// <summary>
            /// This field will be use to get values from context object (context can be DB Context or any other object values will 
            /// be stored). Ideally, it'll be Pluralize form of RentrunTypeEntityName but can be changed.
            /// <para>For example, context.Hobbies in GetHobbiesByPersonIdAsync method.</para>
            /// </summary>
            public string ContextProtpertyName;
            /// <summary>
            /// If return result is array of objects of ReturnTypeBaseEntity then this will be true otherwise if return result is an 
            /// object then it will be false.
            /// </summary>
            public bool IsArray;
            public bool IsGroupBy = false;
            public List<QueryParameterMapping> ParameterMappings;
        }

        private static Assembly assembly = null;
        private static int generatedFilesCount = 0;
        private static IDictionary<string, LogElement> generatedFilesLog;
        private static string PackageName;
        private static string PackageVersion;
        private static Pluralizer pluralizationService;

        private static CodeCompileUnit targetUnit;
        private static CodeTypeDeclaration typeClass, queryClass, repositoryClass, repositoryInterface;
        private static Dictionary<string, string> dicTabs = new Dictionary<string, string>();
        /// <summary>
        /// Key f
        /// </summary>
        private static Dictionary<string, List<string>> NullablePropertyNames = new Dictionary<string, List<string>>();
        /// <summary>
        /// This field will hold Query class Field Names as parent Dictionary Key and List of QueryParameterMapping object as value.
        /// </summary>
        private static Dictionary<string, QueryRepositoryMethodMapping> dicQueryFieldNamesAndParamsListWithTypes;
        /// <summary>
        /// This field will hold Repository class Method Names as parent Dictionary Key and List of QueryParameterMapping object as value.
        /// </summary>
        private static Dictionary<string, List<QueryParameterMapping>> dicRepositoryMethodNamesAndParamsListWithTypes;

        private static Dictionary<string, LoaderRepositoryMapping> dicRepositoryMethodsForLoader;

        private static Dictionary<string, string> dicEntitiesIdFieldName;

        private static readonly string[] defaulAdditionalNamespacesForGraphQLTypes
            = { "System", "System.Collections.Generic", "GraphQL.DataLoader", "GraphQL.Types",
            "GraphQL.Extension.Types.Filter", "GraphQL.Extension.Types.Pagination", 
            "GraphQL.Extension.Types.Unique", "GraphQL.Extension.Types.Grouping" };
        
        private static readonly string[] defaulAdditionalNamespacesForRepository = { "System", "System.Collections.Generic", "System.Linq",
            "System.Threading", "System.Threading.Tasks", 
            Configuration.ORMType == Configuration.ORMTypes.EF6 ? "System.Data.Entity" 
                : Configuration.ORMType == Configuration.ORMTypes.EFCore ? "Microsoft.EntityFrameworkCore" : "", 
            "Linq.Extension",  "Linq.Extension.Grouping", "Linq.Extension.Unique", "Linq.Extension.Pagination",
            "Linq.Extension.Filter"};

        private static readonly string[] defaulAdditionalNamespacesForGraphQLQuery = { "System", "GraphQL.Types.Relay.DataObjects",
            "GraphQL.Types", "GraphQL.Extension.Types.Filter", "GraphQL.Extension.Types.Pagination",
        "GraphQL.Extension.Types.Unique", "GraphQL.Extension.Types.Grouping"};

        private static void AddField(string fieldName,
            string fieldTypeFullName, MemberAttributes fieldAttributes, ref CodeTypeDeclaration _targetClass)
        {
            // Declare the field.
            CodeMemberField field = new CodeMemberField
            {
                Attributes = fieldAttributes,
                Name = fieldName,
                Type = new CodeTypeReference(fieldTypeFullName)
            };
            //(new CodeSnippetExpression("typeof("+ fieldTypeFullName +")"));
            _targetClass.Members.Add(field);
        }

        private static void AddProperty(string fieldName, string propertyName
            , string propertyTypeFullName, MemberAttributes propertyAttributes, bool hasGet, bool hasSet)
        {

            CodeMemberProperty property = new CodeMemberProperty();
            property.Attributes = propertyAttributes;
            property.Name = propertyName;
            property.HasGet = hasGet;
            property.HasSet = hasGet;
            if (hasGet)
            {
                property.Type = new CodeTypeReference(propertyTypeFullName);
                property.GetStatements.Add(new CodeMethodReturnStatement(
                    new CodeFieldReferenceExpression(
                    new CodeThisReferenceExpression(), fieldName)));
            }
            if (hasSet)
            {

                property.SetStatements.Add(new CodeAssignStatement(
                    new CodeFieldReferenceExpression(
                    new CodeThisReferenceExpression(), fieldName), new CodeSnippetExpression("value")
                    ));
            }
            typeClass.Members.Add(property);
        }

        private static void AddMethod()
        {
            // Declaring a ToString method
            CodeMemberMethod toStringMethod = new CodeMemberMethod();
            toStringMethod.Attributes =
                MemberAttributes.Public | MemberAttributes.Override;
            toStringMethod.Name = "ToString";
            toStringMethod.ReturnType =
                new CodeTypeReference("Task<ILookup<int, GlobalApproverPrimaryApprover>>");

            CodeFieldReferenceExpression widthReference =
                new CodeFieldReferenceExpression(
                new CodeThisReferenceExpression(), "Width");
            CodeFieldReferenceExpression heightReference =
                new CodeFieldReferenceExpression(
                new CodeThisReferenceExpression(), "Height");
            CodeFieldReferenceExpression areaReference =
                new CodeFieldReferenceExpression(
                new CodeThisReferenceExpression(), "Area");

            // Declaring a return statement for method ToString.
            CodeMethodReturnStatement returnStatement =
                new CodeMethodReturnStatement();

            // This statement returns a string representation of the width,
            // height, and area.
            string formattedOutput = "The object:" + Environment.NewLine +
                " width = {0}," + Environment.NewLine +
                " height = {1}," + Environment.NewLine +
                " area = {2}";
            returnStatement.Expression =
                new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression("System.String"), "Format",
                new CodePrimitiveExpression(formattedOutput),
                widthReference, heightReference, areaReference);
            toStringMethod.Statements.Add(returnStatement);
            typeClass.Members.Add(toStringMethod);
        }

        private static void AddTypeConstructor(string typeBaseEntityName, string typeBaseEntityFullName
            , string GraphQLGeneratedTypesClassNamePostfix, PropertyInfo[] properties, FieldInfo[] fields
            , IDictionary<string, string> parameters, IList<string> parameterToFieldAssignmentStatments
            , string dataLoaderPrivateMemberName, string repositoryPrivateMemberName)
        {
            // Declare the constructor
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes =
                MemberAttributes.Public | MemberAttributes.Final;

            string dataLoaderParameterName = "accessor", repositoryParameterName = "repository";

            // Add parameters.            
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var p in parameters)
                {
                    if (p.Key.ToLower().Contains("dataloader"))
                        dataLoaderParameterName = p.Value;
                    else
                        repositoryParameterName = p.Value;
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(p.Key, p.Value));
                }
            }
            if (parameterToFieldAssignmentStatments != null && parameterToFieldAssignmentStatments.Count > 0)
            {
                foreach (var statement in parameterToFieldAssignmentStatments)
                {
                    constructor.Statements.Add(new CodeSnippetExpression(statement));
                }
            }
            constructor.Statements.Add(new CodeSnippetStatement(""));

            // Add field initialization logic

            constructor.Statements.Add(new CodeSnippetExpression("Name = nameof(" + typeBaseEntityName
                + GraphQLGeneratedTypesClassNamePostfix + ")"));
            constructor.Statements.Add(new CodeSnippetStatement(""));

            bool isGenericType, isCustomObjectType;
            string fieldBaseEntityFullName, fieldBaseEntityName, fieldGraphQLTypeName, fieldGenericReturnType, batchLoaderMethodName,
                fieldName, propertyName, repositoryMethodByClause, IdFieldName = "Id";
            List<PropertyInfo> PkProperties = null;
            Dictionary<PropertyInfo, ForeignKeyAttribute> FkProperties = null;

            IdFieldName = getEntityIdFieldName(typeBaseEntityName, properties, out PropertyInfo IdFieldProperty);

            if (Configuration.UseDataAnnotationsToFindKeys)
            {
                getPrimaryAndForiegnKeyProperties(properties, out PkProperties, out FkProperties);
                if (PkProperties != null && PkProperties.Count > 0)
                {
                    IdFieldProperty = PkProperties[0];
                    IdFieldName = PkProperties[0].Name;

                    if (!dicEntitiesIdFieldName.ContainsKey(typeBaseEntityName))
                        dicEntitiesIdFieldName.Add(typeBaseEntityName, IdFieldProperty.Name);
                    else
                        dicEntitiesIdFieldName[typeBaseEntityName] = IdFieldProperty.Name;
                }
            }
            
            
            /* Commenting this as we'll not use Parent Id's and bool fields as paremeters for repository methods, rather we'll use
             * SearhInputType and PaginationInputType parameters (from GraphQL.Extension package) only.
             */
            var ParentEntityIdFields = properties.Where(x =>
                  //x.PropertyType.IsClass && !x.PropertyType.FullName.Contains("System") &&
                  (
                     x.PropertyType.FullName.Contains("Int") &&
                     x.Name.ToLower().EndsWith("id") && x.Name != IdFieldName
                  )
                  ||
                  (
                     x.PropertyType.FullName.ToLower().Contains("boolean")
                  )
                 )
             .ToList();

            var typeBaseEntityNamePlural = getPluralizedValue(typeBaseEntityName);
            var typeBaseEntityNameSingular = pluralizationService.Singularize(typeBaseEntityName);
            //List<QueryParameterMapping> queryParameterMappings = new List<QueryParameterMapping>();

            if (!dicQueryFieldNamesAndParamsListWithTypes.ContainsKey(typeBaseEntityNameSingular)
                && (Configuration.ViewNameFilter == null || !Configuration.ViewNameFilter.IsMatch(typeBaseEntityNameSingular)))
            {
                dicQueryFieldNamesAndParamsListWithTypes
                    .Add(typeBaseEntityNameSingular, new QueryRepositoryMethodMapping
                    {
                        ReturnTypeBaseEntityName = typeBaseEntityName,
                        ReturnTypeBaseEntityFullName = typeBaseEntityFullName,
                        IsArray = false,
                        ContextProtpertyName = typeBaseEntityNamePlural,
                        ParameterMappings =
                        new List<QueryParameterMapping>
                        {
                            new QueryParameterMapping
                            {
                                ParameterName = IdFieldName,
                                GraphQLParameterType = IdFieldProperty.PropertyType.Name.Contains("64") ? "LongGraphType": "IntGraphType",
                                IsNullable = false,
                                CSharpParameterType = IdFieldProperty.PropertyType
                            }
                        }
                    });
            }
            if (!dicQueryFieldNamesAndParamsListWithTypes.ContainsKey(typeBaseEntityNamePlural))
            {
                dicQueryFieldNamesAndParamsListWithTypes.Add(typeBaseEntityNamePlural, new QueryRepositoryMethodMapping
                {
                    ReturnTypeBaseEntityName = typeBaseEntityName,
                    ReturnTypeBaseEntityFullName = typeBaseEntityFullName,
                    IsArray = true,
                    ContextProtpertyName = typeBaseEntityNamePlural,
                    ParameterMappings =
                        new List<QueryParameterMapping>()
                        {
                            new QueryParameterMapping
                            {
                                ParameterName = "pagination", GraphQLParameterType = "PaginationInputType", CSharpParameterType = null,
                                IsNullable = true
                            },
                            new QueryParameterMapping
                            {
                                ParameterName = "search", GraphQLParameterType = "SearchInputType", CSharpParameterType = null,
                                IsNullable = true
                            },
                            new QueryParameterMapping
                            {
                                ParameterName = "distinctBy", GraphQLParameterType = "DistinctByInputType", CSharpParameterType = null,
                                IsNullable = true
                            },
                            new QueryParameterMapping
                            {
                                ParameterName = "distinct", GraphQLParameterType = "BooleanGraphType", CSharpParameterType = null,
                                IsNullable = true
                            },
                            //new QueryParameterMapping
                            //{
                            //    ParameterName = "groupBy", GraphQLParameterType = "GroupByInputType", CSharpParameterType = null,
                            //    IsNullable = true
                            //}
                        }
                });
            }
            string totalTypeBaseEntityNamePluralGroupBy = "Total" + typeBaseEntityNamePlural + "GroupBy";

            if (!dicQueryFieldNamesAndParamsListWithTypes.ContainsKey(totalTypeBaseEntityNamePluralGroupBy))
            {
                dicQueryFieldNamesAndParamsListWithTypes.Add(totalTypeBaseEntityNamePluralGroupBy, new QueryRepositoryMethodMapping
                {
                    ReturnTypeBaseEntityName = "IntGraph",
                    ReturnTypeBaseEntityFullName = "int",
                    IsArray = false,
                    IsGroupBy = true,
                    ContextProtpertyName = typeBaseEntityNamePlural,
                    ParameterMappings =
                        new List<QueryParameterMapping>()
                        {
                            //new QueryParameterMapping
                            //{
                            //    ParameterName = "pagination", GraphQLParameterType = "PaginationInputType", CSharpParameterType = null,
                            //    IsNullable = true
                            //},
                            new QueryParameterMapping
                            {
                                ParameterName = "search", GraphQLParameterType = "SearchInputType", CSharpParameterType = null,
                                IsNullable = true
                            },
                            //new QueryParameterMapping
                            //{
                            //    ParameterName = "distinctBy", GraphQLParameterType = "DistinctByInputType", CSharpParameterType = null,
                            //    IsNullable = true
                            //},
                            //new QueryParameterMapping
                            //{
                            //    ParameterName = "distinct", GraphQLParameterType = "BooleanGraphType", CSharpParameterType = null,
                            //    IsNullable = true
                            //},
                            new QueryParameterMapping
                            {
                                ParameterName = "groupBy", GraphQLParameterType = "GroupByInputType", CSharpParameterType = null,
                                IsNullable = true
                            }
                        }
                });
            }

            string groupByOperationOnEntityName = "GroupByOperationOn" + typeBaseEntityNamePlural;

            if (!dicQueryFieldNamesAndParamsListWithTypes.ContainsKey(groupByOperationOnEntityName))
            {
                dicQueryFieldNamesAndParamsListWithTypes.Add(groupByOperationOnEntityName, new QueryRepositoryMethodMapping
                {
                    ReturnTypeBaseEntityName = "GroupValuePair",
                    ReturnTypeBaseEntityFullName = "GroupValuePair",
                    IsArray = true,
                    IsGroupBy = true,
                    ContextProtpertyName = typeBaseEntityNamePlural,
                    ParameterMappings =
                        new List<QueryParameterMapping>()
                        {                            
                            new QueryParameterMapping
                            {
                                ParameterName = "groupByOperationOn", GraphQLParameterType = "GroupByOperationOnInputType", CSharpParameterType = null,
                                IsNullable = true
                            }
                        }
                });
            }
            //dicRepositoryMethodNamesAndParamsListWithTypes.Add(typeBaseEntityName, new List<QueryParameterMapping>());
            //dicRepositoryMethodNamesAndParamsListWithTypes.Add(typeBaseEntityNamePlural, new List<QueryParameterMapping>());

            /* Commenting this as we'll not use Parent Id's and bool fields as paremeters for repository methods, rather we'll use
             * SearhInputType and PaginationInputType parameters (from GraphQL.Extension package) only.
            foreach (var parentField in ParentEntityIdFields)
            {
                dicQueryFieldNamesAndParamsListWithTypes[typeBaseEntityNamePlural].ParameterMappings.Add(new QueryParameterMapping
                {
                    GraphQLParameterType = parentField.PropertyType.FullName.ToLower().Contains("boolean") ? "BooleanGraphType"
                    : "IntGraphType",
                    CSharpParameterType = parentField.PropertyType.FullName.ToLower().Contains("boolean") ? typeof(Boolean?) : typeof(Int32?)
                ,
                    ParameterName = parentField.Name
                });
            }
            */

            NullablePropertyNames.Add(typeBaseEntityName, new List<string>());
            string nullableString = string.Empty;
            PropertyInfo matchingParentIdField = null;
            Type matchingParentIdPropertyType = null;
            string fieldArguments = "";// "arguments:\r" + dicTabs["tab4"] + "new QueryArguments(";
            fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<PaginationInputType>(\"pagination\")";
            fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<SearchInputType>(\"search\")";
            fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<DistinctByInputType>(\"distinctBy\")";
            fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<BooleanGraphType>(\"distinct\")\r" + dicTabs["tab3"];            
            //fieldArguments += "\r" + dicTabs["tab4"] + "),\r" + dicTabs["tab4"];

            foreach (var prop in properties)
            {                

                if (!Configuration.UseDataAnnotationsToFindKeys || FkProperties == null || FkProperties.Count < 1)
                    matchingParentIdField = ParentEntityIdFields.Where(x => x.Name.StartsWith(prop.Name))
                        .FirstOrDefault();
                else if (FkProperties != null && FkProperties.Count > 0)
                {
                    var fkAttribute = FkProperties.Where(x => x.Key.Name == prop.Name)
                        .Select(x => x.Value).FirstOrDefault();
                    if (fkAttribute != null)
                        matchingParentIdField = properties.Where(x => x.Name == fkAttribute.Name).FirstOrDefault();
                }
                if (matchingParentIdField != null)
                    matchingParentIdPropertyType = matchingParentIdField.PropertyType.GetUnderlyingType();
                if ((prop.PropertyType.IsGenericType && prop.PropertyType.FullName.ToLower().Contains("nullable"))
                    || prop.PropertyType.FullName.ToLower().Contains("string")
                    || (Configuration.MakeAllFieldsOfViewNullable && Configuration.ViewNameFilter != null 
                        && Configuration.ViewNameFilter.IsMatch(typeBaseEntityNameSingular)))
                {
                    NullablePropertyNames[typeBaseEntityName].Add(prop.Name);
                    nullableString = ", nullable: true";
                }
                else
                    nullableString = "";
                propertyName = prop.Name;
                fieldName = Utility.getCamelCaseString(prop.Name);
                isGenericType = prop.PropertyType.IsGenericType;
                isCustomObjectType = ((prop.PropertyType.IsClass || prop.PropertyType.IsInterface)
                        && (!prop.PropertyType.FullName.ToLower().Contains("system.") || prop.PropertyType.IsGenericType));
                fieldBaseEntityName = isGenericType ? prop.PropertyType.GenericTypeArguments[0].Name
                                            : prop.PropertyType.Name;
                fieldBaseEntityName = fieldBaseEntityName.Replace("+", "_");

                fieldBaseEntityFullName = isGenericType ? prop.PropertyType.GenericTypeArguments[0].FullName
                                            : prop.PropertyType.FullName;
                fieldBaseEntityFullName = fieldBaseEntityFullName.Replace("+", "_");

                fieldGraphQLTypeName = (isGenericType ? prop.PropertyType.GenericTypeArguments[0].Name
                                        : prop.PropertyType.Name) + GraphQLGeneratedTypesClassNamePostfix;
                fieldGenericReturnType =
                    (isGenericType ? ("ListGraphType<" + fieldGraphQLTypeName + ">, IEnumerable<" + fieldBaseEntityFullName + ">")
                    : fieldGraphQLTypeName + ", " + fieldBaseEntityFullName);
                batchLoaderMethodName = "GetOrAdd" + (isGenericType ? "Collection" : "") + "BatchLoader";
                repositoryMethodByClause = isGenericType ? typeBaseEntityName : "";

                if (prop.PropertyType.FullName.ToString().ToLower().Contains("int16"))
                {
                    constructor.Statements.Add(new CodeSnippetExpression(
                        "Field<IntGraphType>(\"" + fieldName + "\", resolve: context => context.Source." + prop.Name + ")"
                        ));

                }
                else if (!isCustomObjectType)
                {
                    /*
                    if ((prop.PropertyType.IsGenericType
                        && prop.PropertyType.FullName.ToLower().Contains("nullable")
                        || prop.PropertyType.FullName.ToLower().Contains("string")))
                    {
                        constructor.Statements.Add(new CodeSnippetExpression
                            ("Field(x => x." + prop.Name + ", nullable: true)"));
                    }
                    else */
                    if (prop.PropertyType.FullName.ToLower().Contains("byte"))
                    {
                        constructor.Statements.Add(new CodeSnippetExpression
                            ("Field<StringGraphType>(\"" + Utility.getCamelCaseString(prop.Name) + "\" " + nullableString
                            + ", resolve: context => "
                            + (prop.PropertyType.FullName.Contains("[]")
                            ? "System.Text.Encoding.Default.GetString(context.Source." + prop.Name
                            : "System.Convert.ToString(" + prop.Name) + "))"));
                    }
                    else if (prop.PropertyType.FullName.ToLower().Contains("date"))
                        constructor.Statements.Add(new CodeSnippetExpression("Field(x => x." + prop.Name + nullableString
                            + ", type: typeof(DateTimeGraphType))"));
                    else
                        constructor.Statements.Add(new CodeSnippetExpression("Field(x => x." + prop.Name + nullableString + ")"));
                }
                else
                {
                    string PkOrFkIdFieldName = Configuration.UseDataAnnotationsToFindKeys ?
                        getPkOrFkFieldName(typeBaseEntityFullName, typeBaseEntityName, prop)
                        : repositoryMethodByClause + "Id";
                    constructor.Statements.Add(new CodeSnippetStatement(""));                    

                    string batchLoaderParams = isGenericType ? "ids" : "ids, token";
                    constructor.Statements.Add(new CodeSnippetExpression(
                     "Field<" + fieldGenericReturnType + ">(\""
                    + fieldName + "\")" + fieldArguments
                    + ".ResolveAsync(context => \r" + dicTabs["tab3"] + "{\r" + dicTabs["tab4"]
                    + "Dictionary<string, object> args = new Dictionary<string, object>(); "
                    + "\r" + dicTabs["tab4"]
                    + "args.Add(\"pagination\", context.GetArgument<object>(\"pagination\"));"
                    + "\r" + dicTabs["tab4"]
                    + "args.Add(\"search\", context.GetArgument<object>(\"search\"));"
                    + "\r" + dicTabs["tab4"]
                    + "args.Add(\"distinctBy\", context.GetArgument<object>(\"distinctBy\"));"
                    + "\r" + dicTabs["tab4"]
                    + "args.Add(\"distinct\", context.GetArgument<bool>(\"distinct\"));"
                    + "\r" + dicTabs["tab4"]
                    + (!isGenericType && matchingParentIdField != null && matchingParentIdField.PropertyType.FullName.ToLower().Contains("nullable")
                        ? "if (context.Source." + matchingParentIdField?.Name + " != null)\r" + dicTabs["tab4"]
                    + "{\r" + dicTabs["tab5"] : "")
                    + "var loader = this." + dataLoaderPrivateMemberName + ".Context." + batchLoaderMethodName + "<"
                    + (isGenericType ? IdFieldProperty?.PropertyType.Name : matchingParentIdPropertyType.Name)
                    + ", " + fieldBaseEntityFullName + ">\r" + dicTabs["tab5"]
                    + "($\"Get" + propertyName + "By" + /*repositoryMethodByClause + "Id*/ PkOrFkIdFieldName + "[{context.SubFields}]\", (" + batchLoaderParams
                    + ") => " + "\r" + dicTabs["tab5"] + "this." + repositoryPrivateMemberName + ".Get" + propertyName
                    + "By" + /*repositoryMethodByClause + "Id*/ PkOrFkIdFieldName + "Async(args, context.SubFields.Keys, " + batchLoaderParams + "));\r"
                    + (!isGenericType && matchingParentIdField.PropertyType.FullName.ToLower().Contains("nullable") ? dicTabs["tab5"] : dicTabs["tab4"])
                    + "return loader.LoadAsync(("
                    + (isGenericType ? IdFieldProperty.PropertyType.Name : matchingParentIdPropertyType.Name)
                    + ") context.Source."
                    + (isGenericType ? IdFieldName : matchingParentIdField.Name) + ");"
                    + (!isGenericType && matchingParentIdField.PropertyType.FullName.ToLower().Contains("nullable")
                        ? "\r " + dicTabs["tab4"] + "}\r" + dicTabs["tab4"] + "else\r" + dicTabs["tab5"] + "return null;\r" + dicTabs["tab3"]
                    : "\r" + dicTabs["tab3"])
                    + "})"

                    ));
                    constructor.Statements.Add(new CodeSnippetStatement(""));

                    if (!dicRepositoryMethodsForLoader.ContainsKey("Get" + propertyName + "By" + PkOrFkIdFieldName + "Async"))
                    {
                        
                        dicRepositoryMethodsForLoader.Add("Get" + propertyName + "By" + PkOrFkIdFieldName + "Async"
                            , new LoaderRepositoryMapping
                            {
                                ReturnTypeBaseEntityFullName = fieldBaseEntityFullName
                            ,
                                ReturnTypeBaseEntityName = fieldBaseEntityName,
                                IsByParent = isGenericType
                            ,
                                ContextProtpertyName = //propertyName 
                                    getPluralizedValue(propertyName)
                            //getPluralizedValue(fieldBaseEntityName) 
                            ,
                                IdsParamerterName = Configuration.UseDataAnnotationsToFindKeys ? PkOrFkIdFieldName
                                    : (isGenericType ? typeBaseEntityName : fieldBaseEntityName) + "Ids"
                                ,
                                WhereClauseIdFieldName = Configuration.UseDataAnnotationsToFindKeys ? PkOrFkIdFieldName
                                    : (isGenericType ? typeBaseEntityName : "") + "Id"
                                /*
                                IdsParamerterName = (isGenericType? fkPropAndAttribute.Value.Name : IdFieldName)
                                //(isGenericType ? typeBaseEntityName : fieldBaseEntityName) + "Ids"
                            ,
                                WhereClauseIdFieldName = (isGenericType ? fkPropAndAttribute.Value.Name : IdFieldName)
                                //(isGenericType ? typeBaseEntityName : "") + "Id"
                                */
                            ,
                                PropertyType = "int"
                            });
                    }
                }
            }
            

            if (!string.IsNullOrEmpty(Configuration.TypeClasses.AdditionalCodeToBeAddedInConstructor))
            {
                var additionCodeStatements = Configuration.TypeClasses.AdditionalCodeToBeAddedInConstructor.TrimEnd(';').Split(';');
                foreach (var statement in additionCodeStatements)
                {
                    constructor.Statements.Add(new CodeSnippetExpression(statement.Trim()));
                    constructor.Statements.Add(new CodeSnippetStatement(""));
                }
            }

            typeClass.Members.Add(constructor);
        }

        private static void CreateTypeClasses(string assemblyNameAndPathAndExtension, string typesNamespaceValue)
        {
            string classPostfix, fileExtension, outputpath;
            classPostfix = Configuration.TypeClasses.ClassSuffix;
            fileExtension = Configuration.TypeClasses.FileExtension;
            outputpath = Configuration.TypeClasses.Outputpath;

            DirectoryInfo dInfo = new DirectoryInfo(outputpath);
            if (!dInfo.Exists)
            {
                Directory.CreateDirectory(outputpath);
            }

            CodeNamespace typesNamespace = new CodeNamespace(typesNamespaceValue);
            foreach (string ns in defaulAdditionalNamespacesForGraphQLTypes)
            {
                typesNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
            }

            if (!string.IsNullOrEmpty(Configuration.TypeClasses.AdditionalNamespaces))
            {
                var additionalNamespaces = Configuration.TypeClasses.AdditionalNamespaces.Split(',');
                foreach (var ns in additionalNamespaces)
                {
                    typesNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }
            }

            assembly = Assembly.LoadFrom(assemblyNameAndPathAndExtension);
            var types = assembly.GetTypes()
                .Where(x => !x.IsInterface && !string.IsNullOrEmpty(x.Name) && !string.IsNullOrEmpty(x.Namespace) &&
                    (
                        (
                            (Configuration.TypeClasses.EntityClassesNamespacesInclude != null
                            && Configuration.TypeClasses.EntityClassesNamespacesInclude.IsMatch(x.Namespace)
                            )
                            || Configuration.TypeClasses.EntityClassesNamespacesInclude == null
                        )
                        &&
                        (
                            (Configuration.TypeClasses.EntityClassesNamespacesExclude != null
                            && !Configuration.TypeClasses.EntityClassesNamespacesExclude.IsMatch(x.Namespace)
                            )
                            || Configuration.TypeClasses.EntityClassesNamespacesExclude == null
                        )
                        &&
                        (
                            (Configuration.TypeClasses.EntityClassNamesInclude != null
                            && Configuration.TypeClasses.EntityClassNamesInclude.IsMatch(x.Name)
                            )
                            || Configuration.TypeClasses.EntityClassNamesInclude == null
                        )
                        &&
                        (
                            (Configuration.TypeClasses.EntityClassNamesExclude != null
                            && !Configuration.TypeClasses.EntityClassNamesExclude.IsMatch(x.Name)
                            )
                            || Configuration.TypeClasses.EntityClassNamesExclude == null
                        )
                        && !x.Name.ToLower().Contains("dbcontext")
                    )
                );
            string typeName = "";
            foreach (var tc in types)
            {
                try
                {
                    if (tc.FullName.Contains('+'))
                    {
                        typeName = tc.FullName.Substring(tc.FullName.LastIndexOf('.') + 1);
                        typeName = typeName.Replace('+', '_');
                    }
                    else
                        typeName = tc.Name;

                    if (!generatedFilesLog.ContainsKey(typeName + classPostfix + fileExtension))
                    {
                        generatedFilesLog.Add(typeName + classPostfix + fileExtension, new LogElement());
                    }
                    typesNamespace.Types.Clear();
                    if (typesNamespace.Comments.Count < 1)
                        typesNamespace = addCopyRightComments(typesNamespace);

                    typeClass = new CodeTypeDeclaration(typeName + classPostfix);
                    typeClass.BaseTypes.Add("ObjectGraphType<" + tc.FullName.Replace('+', '.') + ">");
                    typeClass.IsClass = true;
                    typeClass.IsPartial = true;
                    typeClass.TypeAttributes =
                        TypeAttributes.Public;
                    if (!string.IsNullOrEmpty(Configuration.TypeClasses.PrivateMembers))
                    {
                        var members = Configuration.TypeClasses.PrivateMembers.Split(',');
                        foreach (var member in members)
                        {
                            addClassPrivateMember(member, ref typeClass);
                        }
                    }

                    var dataLoaderPrivateMemberName = addClassPrivateMember(Configuration.TypeClasses.DataLoaderPrivateMember
                        , ref typeClass);
                    var repositoryPrivateMemberName = addClassPrivateMember(Configuration.TypeClasses.RepositoryPrivateMember
                        , ref typeClass);
                    IList<string> parameterToFieldAssignmentStatments = new List<string>();
                    IDictionary<string, string> constructorParameters = new Dictionary<string, string>();

                    if (!string.IsNullOrEmpty(Configuration.TypeClasses.DataLoaderConstructorParameter))
                    {
                        var typeAndVariableForDataLoaderParameter =
                            Configuration.TypeClasses.DataLoaderConstructorParameter.Split(' ');
                        if (typeAndVariableForDataLoaderParameter.Length == 2)
                        {
                            constructorParameters.Add(typeAndVariableForDataLoaderParameter[0], typeAndVariableForDataLoaderParameter[1]);
                            if (!string.IsNullOrEmpty(dataLoaderPrivateMemberName) && dataLoaderPrivateMemberName != "member is empty")
                            {
                                parameterToFieldAssignmentStatments.Add("this." + dataLoaderPrivateMemberName + " = "
                                    + typeAndVariableForDataLoaderParameter[1]);
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(Configuration.TypeClasses.RepositoryConstructorParameter))
                    {
                        var typeAndVariableForRepositoryParameter =
                            Configuration.TypeClasses.RepositoryConstructorParameter.Split(' ');
                        if (typeAndVariableForRepositoryParameter.Length == 2)
                        {
                            constructorParameters.Add(typeAndVariableForRepositoryParameter[0], typeAndVariableForRepositoryParameter[1]);
                            if (!string.IsNullOrEmpty(repositoryPrivateMemberName) && repositoryPrivateMemberName != "member is empty")
                            {
                                parameterToFieldAssignmentStatments.Add("this." + repositoryPrivateMemberName + " = "
                                    + typeAndVariableForRepositoryParameter[1]);
                            }
                        }
                    }

                    AddTypeConstructor(typeName, tc.FullName.Replace('+', '.'), classPostfix, tc.GetProperties(), null, constructorParameters
                        , parameterToFieldAssignmentStatments, dataLoaderPrivateMemberName, repositoryPrivateMemberName);

                    typesNamespace.Types.Add(typeClass);
                    targetUnit.Namespaces.Clear();
                    targetUnit.Namespaces.Add(typesNamespace);

                    GenerateCSharpCode(typeName + classPostfix + fileExtension, outputpath);
                }
                catch (Exception ex)
                {
                    var logElement = new LogElement();
                    logElement.exception = ex;
                    logElement.isException = true;

                    if (!generatedFilesLog.ContainsKey(typeName + classPostfix + fileExtension))
                    {
                        generatedFilesLog.Add(typeName + classPostfix + fileExtension, logElement);
                    }
                }
            }
        }

        private static void AddQueryConstructor(IDictionary<string, string> parameters,
            IList<string> parameterToFieldAssignmentStatments, string repositoryPrivateMemberName)
        {
            // Declare the constructor
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes =
                MemberAttributes.Public | MemberAttributes.Final;

            // Add parameters.            
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var p in parameters)
                {
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(p.Key, p.Value));
                }
            }
            if (parameterToFieldAssignmentStatments != null && parameterToFieldAssignmentStatments.Count > 0)
            {
                foreach (var statement in parameterToFieldAssignmentStatments)
                {
                    constructor.Statements.Add(new CodeSnippetExpression(statement));
                }
            }

            constructor.Statements.Add(new CodeSnippetExpression("Name = nameof(" + queryClass.Name + ")"));
            constructor.Statements.Add(new CodeSnippetStatement(""));


            string returnStatment, fieldName, fieldArguments = "", resolver = "", resolverReturn = "";

            foreach (var m in dicQueryFieldNamesAndParamsListWithTypes)
            {
                fieldArguments = "";
                resolver = ".ResolveAsync(async context => " + "\r" + dicTabs["tab3"] +
                    "{";
                    

                resolverReturn = "return await this." + repositoryPrivateMemberName + ".Get" + m.Key + "(";
                if (m.Value.ParameterMappings.Count > 0)
                {
                    bool isContextParamAdded = false;
                    //resolver += "\r" + dicTabs["tab5"] ;
                    fieldArguments = "";// "arguments:\r" + dicTabs["tab4"] + "new QueryArguments(";
                    foreach (var p in m.Value.ParameterMappings)
                    {
                        fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<" + (p.IsNullable ? p.GraphQLParameterType
                            : "NonNullGraphType<" + p.GraphQLParameterType + ">") + ">(\""
                            + Utility.getCamelCaseString(p.ParameterName) + "\")";
                        //resolver +=;
                        if (p.CSharpParameterType == null)
                        {
                            if (!isContextParamAdded)
                                resolver += "\r" + dicTabs["tab4"] + 
                                    "Dictionary<string, object> args = new Dictionary<string, object>(); ";
                            resolver += "\r" + dicTabs["tab4"] +
                                (p.GraphQLParameterType == "BooleanGraphType" ? "bool" : "object") + " " + Utility.getCamelCaseString(p.ParameterName) 
                                + " = context.GetArgument<" + (p.GraphQLParameterType == "BooleanGraphType" ? "bool" : "object") + ">(\"" 
                                + Utility.getCamelCaseString(p.ParameterName) + "\");";
                            resolver += "\r" + dicTabs["tab4"] +
                                "args.Add(\"" + Utility.getCamelCaseString(p.ParameterName) + "\", " 
                                + Utility.getCamelCaseString(p.ParameterName) + ");";
                            if (!isContextParamAdded)
                            {
                                resolverReturn += "\r" + dicTabs["tab5"] + "args" + (!m.Value.IsGroupBy ? ", context.SubFields.Keys" : "" ) +");";                                
                                isContextParamAdded = true;
                            }
                        }
                        else
                            resolverReturn += "\r" + dicTabs["tab5"] + "context.GetArgument<" + p.CSharpParameterType.Name
                            + ">(\"" + Utility.getCamelCaseString(p.ParameterName) + "\"), context.SubFields.Keys);";
                        /* Commenting this as we'll not use Parent Id's and bool fields as paremeters for repository methods, rather we'll use
         * SearhInputType and PaginationInputType parameters (from GraphQL.Extension package) only.
                        + ((p.CSharpParameterType.IsGenericType && p.CSharpParameterType.GenericTypeArguments[0].Name == "Boolean")
                        ?
                        (
                            "context.HasArgument(\"" + Utility.getCamelCaseString(p.ParameterName) + "\") ? context.Arguments[\"" 
                            + Utility.getCamelCaseString(p.ParameterName) + "\"] : null,"
                        )
                        :
                        ("context.GetArgument<" + (p.CSharpParameterType.IsGenericType ? p.CSharpParameterType.GenericTypeArguments[0].Name
                            : p.CSharpParameterType.Name) + ">(\"" + Utility.getCamelCaseString(p.ParameterName) + "\"),"
                        )                            
                    );*/
                    }
                    //resolver = resolver.TrimEnd(',');
                    //fieldArguments = fieldArguments.TrimEnd(',');
                    //fieldArguments += "\r" + dicTabs["tab4"] + "),\r" + dicTabs["tab4"];
                }
                resolver += "\r" + dicTabs["tab4"] + resolverReturn;
                resolver += "\r" + dicTabs["tab3"] + "})";
                fieldName = Utility.getCamelCaseString(
                    //m.Value.IsArray ? pluralizationService.Pluralize(m.Value.ReturnTypeBaseEntityName) : m.Value.ReturnTypeBaseEntityName);
                    m.Key);
                returnStatment = "Field<" + (m.Value.IsArray ? "ListGraphType<" : "")
                    + m.Value.ReturnTypeBaseEntityName + Configuration.TypeClasses.ClassSuffix
                    + (m.Value.IsArray ? ">>" : ">") ;

                // + "resolve: async context => \r" + dicTabs["tab4"] + "{\r" + dicTabs["tab5"]
                constructor.Statements.Add(new CodeSnippetExpression(returnStatment + "(\""
                    + fieldName + "\")" + fieldArguments + "\r" + dicTabs["tab3"] + resolver 
                        ));
                constructor.Statements.Add(new CodeSnippetStatement(""));

            } //End of All methods foreach.

            queryClass.Members.Add(constructor);
        }
        private static void CreateQueryClass()
        {
            string fileExtension, outputpath, queryNamespaceValue, queryClassName;
            fileExtension = Configuration.QueryClass.FileExtension;
            outputpath = Configuration.QueryClass.Outputpath;
            queryNamespaceValue = Configuration.QueryClass.QueryClassNamespace;
            queryClassName = Configuration.QueryClass.QueryClassName;

            CodeNamespace queryNamespace = new CodeNamespace(queryNamespaceValue);

            try
            {
                generatedFilesLog.Add(queryClassName + fileExtension, new LogElement());
                DirectoryInfo dInfo = new DirectoryInfo(outputpath);
                if (!dInfo.Exists)
                {
                    Directory.CreateDirectory(outputpath);
                }

                foreach (string ns in defaulAdditionalNamespacesForGraphQLQuery)
                {
                    queryNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }

                if (!string.IsNullOrEmpty(Configuration.QueryClass.AdditionalNamespaces))
                {
                    var additionalNamespaces = Configuration.QueryClass.AdditionalNamespaces.Split(',');
                    foreach (var ns in additionalNamespaces)
                    {
                        queryNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                    }
                }
                if (!string.IsNullOrEmpty(Configuration.TypeClasses.TypeClassesNamespace))
                    queryNamespace.Imports.Add(new CodeNamespaceImport(Configuration.TypeClasses.TypeClassesNamespace));
                if (!string.IsNullOrEmpty(Configuration.RepositoryClass.RepositoryClassesNamespace))
                    queryNamespace.Imports.Add(new CodeNamespaceImport(Configuration.RepositoryClass.RepositoryClassesNamespace));
                if (queryNamespace.Comments.Count < 1)
                    queryNamespace = addCopyRightComments(queryNamespace);
                queryClass = new CodeTypeDeclaration(queryClassName);
                queryClass.BaseTypes.Add("ObjectGraphType");
                queryClass.IsClass = true;
                queryClass.IsPartial = true;
                queryClass.TypeAttributes = TypeAttributes.Public;

                var repositoryPrivateMemberName = addClassPrivateMember(Configuration.QueryClass.RepositoryPrivateMember
                            , ref queryClass);

                IList<string> parameterToFieldAssignmentStatments = new List<string>();
                IDictionary<string, string> constructorParameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(Configuration.QueryClass.RepositoryConstructorParameter))
                {
                    var typeAndVariableForRepositoryParameter =
                        Configuration.QueryClass.RepositoryConstructorParameter.Split(' ');
                    if (typeAndVariableForRepositoryParameter.Length == 2)
                    {
                        constructorParameters.Add(typeAndVariableForRepositoryParameter[0], typeAndVariableForRepositoryParameter[1]);
                        if (!string.IsNullOrEmpty(repositoryPrivateMemberName) && repositoryPrivateMemberName != "member is empty")
                        {
                            parameterToFieldAssignmentStatments.Add("this." + repositoryPrivateMemberName + " = "
                                + typeAndVariableForRepositoryParameter[1]);
                        }
                    }
                }
                AddQueryConstructor(constructorParameters, parameterToFieldAssignmentStatments, repositoryPrivateMemberName);

         
                queryNamespace.Types.Add(queryClass);
                targetUnit.Namespaces.Clear();
                targetUnit.Namespaces.Add(queryNamespace);
                GenerateCSharpCode(queryClassName + fileExtension, outputpath);
            }
            catch (Exception ex)
            {
                var logElement = generatedFilesLog[queryClassName + fileExtension];
                logElement.exception = ex;
                logElement.isException = true;
            }
        }
        private static void AddRepositoryConstructor(IDictionary<string, string> parameters,
            IList<string> parameterToFieldAssignmentStatments)
        {
            // Declare the constructor
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes =
                MemberAttributes.Public | MemberAttributes.Final;

            // Add parameters.            
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var p in parameters)
                {
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(p.Key, p.Value));
                }
            }
            if (parameterToFieldAssignmentStatments != null && parameterToFieldAssignmentStatments.Count > 0)
            {
                foreach (var statement in parameterToFieldAssignmentStatments)
                {
                    constructor.Statements.Add(new CodeSnippetExpression(statement));
                }
            }
            //constructor.Statements.Add(new CodeSnippetStatement(""));
            repositoryClass.Members.Add(constructor);
        }
        private static void CreateRepositoryClass(string repositoryNamespaceValue)
        {
            string fileExtension, outputpath;
            fileExtension = Configuration.RepositoryClass.FileExtension;
            outputpath = Configuration.RepositoryClass.Outputpath;
            string repositoryClassName, repositoryInterfaceName;
            repositoryClassName = Configuration.RepositoryClass.RepositoryClassName;
            repositoryInterfaceName = "I" + Configuration.RepositoryClass.RepositoryClassName;

            CodeNamespace repositoryNamespace = new CodeNamespace(repositoryNamespaceValue);

            try
            {
                generatedFilesLog.Add(repositoryClassName + fileExtension, new LogElement());
                DirectoryInfo dInfo = new DirectoryInfo(outputpath);
                if (!dInfo.Exists)
                {
                    Directory.CreateDirectory(outputpath);
                }


                foreach (string ns in defaulAdditionalNamespacesForRepository)
                {
                    if (!string.IsNullOrEmpty(ns))
                        repositoryNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }

                if (!string.IsNullOrEmpty(Configuration.RepositoryClass.AdditionalNamespaces))
                {
                    var additionalNamespaces = Configuration.RepositoryClass.AdditionalNamespaces.Split(',');
                    foreach (var ns in additionalNamespaces)
                    {
                        repositoryNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                    }
                }

                if (repositoryNamespace.Comments.Count < 1)
                    repositoryNamespace = addCopyRightComments(repositoryNamespace);
                repositoryInterface = new CodeTypeDeclaration(repositoryInterfaceName);
                repositoryInterface.IsInterface = true;
                repositoryInterface.IsPartial = true;
                repositoryClass = new CodeTypeDeclaration(repositoryClassName);
                repositoryClass.BaseTypes.Add(repositoryInterfaceName);
                repositoryClass.IsClass = true;
                repositoryClass.IsPartial = true;
                repositoryClass.TypeAttributes = TypeAttributes.Public;

                var contextPrivateMemberName = addClassPrivateMember(Configuration.RepositoryClass.DBContextPrivateMember
                            , ref repositoryClass);

                IList<string> parameterToFieldAssignmentStatments = new List<string>();
                IDictionary<string, string> constructorParameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(Configuration.RepositoryClass.DBContextConstructorParameter))
                {
                    var typeAndVariableForDBContextParameter =
                        Configuration.RepositoryClass.DBContextConstructorParameter.Split(' ');
                    if (typeAndVariableForDBContextParameter.Length == 2)
                    {
                        constructorParameters.Add(typeAndVariableForDBContextParameter[0], typeAndVariableForDBContextParameter[1]);
                        if (!string.IsNullOrEmpty(contextPrivateMemberName) && contextPrivateMemberName != "member is empty")
                        {
                            parameterToFieldAssignmentStatments.Add("this." + contextPrivateMemberName + " = "
                                + typeAndVariableForDBContextParameter[1]);
                        }
                    }
                }
                AddRepositoryConstructor(constructorParameters, parameterToFieldAssignmentStatments);
                string returnTypePart, statement1, returnStatment;
                bool isParentNullable = false;
                string tabs = "";
                string arrayTypeMethodStatements = "";
                foreach (var m in dicRepositoryMethodsForLoader)
                {
                    var method = new CodeMemberMethod
                    {
                        Attributes = MemberAttributes.Public | MemberAttributes.Final,
                        Name = m.Key + "<E>"
                    };
                    var methodInterface = new CodeMemberMethod
                    {
                        Name = m.Key + "<E>"
                    };
                    m.Value.PropertyType = "int";
                    //NullablePropertyNames[m.Value.ReturnTypeBaseEntityName].Contains(m.Value.WhereClauseIdFieldName)
                    //? "int?" : "int";
                    isParentNullable = NullablePropertyNames[m.Value.ReturnTypeBaseEntityName].Contains(m.Value.WhereClauseIdFieldName);
                    returnStatment = "return Task.FromResult(";

                    statement1 = "var results = this." + contextPrivateMemberName + "." + m.Value.ContextProtpertyName
                        + "\r" + dicTabs["tab3"] + ".Where(x => " + m.Value.IdsParamerterName + ".Contains(x." + m.Value.WhereClauseIdFieldName
                        + "))\r" + dicTabs["tab3"];
                    method.Parameters.Add(new CodeParameterDeclarationExpression("IDictionary<string, object>", "conditionalArguments"));
                    method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                    methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IDictionary<string, object>", "conditionalArguments"));
                    methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));

                    method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<E>", m.Value.IdsParamerterName));
                    methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<E>", m.Value.IdsParamerterName));

                    tabs = $"\r{dicTabs["tab4"]}";
                    arrayTypeMethodStatements = $"var res = this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}"
                    + $"{ tabs}.AsNoTracking(){tabs}";
                    arrayTypeMethodStatements += $".WhereWithDistinctBy(conditionalArguments, " +
                        $"this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}, " +
                        $"{m.Value.IdsParamerterName}, \"{m.Value.WhereClauseIdFieldName}\"){tabs}" +
                        $".Select(selectionFields){tabs}" +
                        $".Pagination(conditionalArguments){tabs}.ToList();";

                    if (m.Value.IsByParent)
                    {
                        arrayTypeMethodStatements += $"\r\r{dicTabs["tab3"]}var results = (IEnumerable<" +
                            $"{ m.Value.ReturnTypeBaseEntityFullName }>) res;\r\r{dicTabs["tab3"]}" +
                            $"return Task.FromResult((ILookup<E, {m.Value.ReturnTypeBaseEntityFullName}>){tabs}" +
                            $"results.ToLookup(i => i.{ m.Value.WhereClauseIdFieldName}{(isParentNullable ? ".Value" : "")}))";

                        returnTypePart = "ILookup<E";
                        /*
                        //statement1 = "var convertedIds = (IEnumerable<" + m.Value.PropertyType + ">) "
                        //    + m.Value.IdsParamerterName + ";\r" + dicTabs["tab3"];
                        statement1 = "var results = this." + contextPrivateMemberName + "." + m.Value.ContextProtpertyName
                        + "\r" + dicTabs["tab3"] + ".Where(x => "+ m.Value.IdsParamerterName + ".Contains(x." + m.Value.WhereClauseIdFieldName
                        + (isParentNullable ? ".Value" : "")
                        + "))\r" + dicTabs["tab3"];

                        statement1 += ".ToList()";
                        returnStatment += "(ILookup<E, " + m.Value.ReturnTypeBaseEntityFullName
                            + ">)\r" + dicTabs["tab4"] +
                            "results.ToLookup(i => i." + m.Value.WhereClauseIdFieldName
                            + (isParentNullable ? ".Value" : "")
                            + "))";
                        */
                    }
                    else
                    {
                        //method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<E>", m.Value.IdsParamerterName));
                        //methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<E>", m.Value.IdsParamerterName));

                        method.Parameters.Add(new CodeParameterDeclarationExpression("CancellationToken", "token"));
                        methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("CancellationToken", "token"));

                        arrayTypeMethodStatements += $"\r\r{dicTabs["tab3"]}var results = ((IEnumerable<" +
                            $"{ m.Value.ReturnTypeBaseEntityFullName }>) res){tabs}.ToDictionary(i => i.{ m.Value.WhereClauseIdFieldName});" +
                            $"\r\r{dicTabs["tab3"]}return Task.FromResult((IDictionary<E, {m.Value.ReturnTypeBaseEntityFullName}>) results)";

                        returnTypePart = "IDictionary<E";
                        //statement1 += $".ToDictionary(i => i.{ m.Value.WhereClauseIdFieldName})";
                        //returnStatment += "(IDictionary<E, " + m.Value.ReturnTypeBaseEntityFullName + ">) results)";
                    }

                    method.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ", " + m.Value.ReturnTypeBaseEntityFullName + ">>\r" + dicTabs["tab3"]);
                    methodInterface.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ", " + m.Value.ReturnTypeBaseEntityFullName + ">>\r" + dicTabs["tab3"]);
                    //method.Statements.Add(new CodeSnippetExpression(statement1));
                    method.Statements.Add(new CodeSnippetExpression(arrayTypeMethodStatements));

                    repositoryClass.Members.Add(method);
                    repositoryInterface.Members.Add(methodInterface);
                }

                foreach (var m in dicQueryFieldNamesAndParamsListWithTypes)
                {
                    var method = new CodeMemberMethod
                    {
                        Attributes = MemberAttributes.Public | MemberAttributes.Final,
                        Name = "Get" + m.Key
                    };
                    var methodInterface = new CodeMemberMethod
                    {
                        Name = "Get" + m.Key
                    };
                    returnTypePart = (m.Value.IsArray ? "IEnumerable<" + m.Value.ReturnTypeBaseEntityFullName + ">"
                        : m.Value.ReturnTypeBaseEntityFullName);
                    returnStatment = "return Task.FromResult<" + returnTypePart
                        + ">(this." + contextPrivateMemberName + "." + m.Value.ContextProtpertyName
                        + "\r" + dicTabs["tab3"];

                    if (m.Value.ParameterMappings.Count < 1)
                    {
                        if (!m.Value.IsArray)
                        {
                            returnStatment += ".FirstOrDefault())";
                        }
                        else
                        {
                            returnStatment += ".ToList())";
                        }
                    }
                    else
                    {
                        Type paramType = null;
                        bool isContextParamAdded = false;
                        foreach (var qp in m.Value.ParameterMappings)
                        {
                            if (qp.CSharpParameterType != null)
                            {
                                paramType = qp.CSharpParameterType.FullName.Contains("System.Boolean") ? typeof(object)
                                        : qp.CSharpParameterType;
                                method.Parameters.Add(new CodeParameterDeclarationExpression
                                (paramType, qp.ParameterName));
                                method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                                methodInterface.Parameters.Add(new CodeParameterDeclarationExpression
                                    (paramType, qp.ParameterName));
                                methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                            }
                            else if (!isContextParamAdded)
                            {
                                method.Parameters.Add(new CodeParameterDeclarationExpression("IDictionary<string, object>", "conditionalArguments"));
                                methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IDictionary<string, object>", "conditionalArguments"));
                                if (!m.Value.IsGroupBy)
                                {
                                    method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                                    methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                                }
                                isContextParamAdded = true;
                            }

                        }
                        if (!m.Value.IsArray && !m.Value.IsGroupBy)
                        {
                            var firstParam = m.Value.ParameterMappings[0];
                            returnStatment += ".Select(selectionFields)\r" + dicTabs["tab3"];
                            returnStatment += ".Where(x => x." + firstParam.ParameterName + " == " + firstParam.ParameterName
                            + ")\r" + dicTabs["tab3"] + ".FirstOrDefault())";
                        }
                        else if (m.Value.IsArray && !m.Value.IsGroupBy)
                        {
                            //method.Comments.Add(new CodeCommentStatement("Generator will only generate code for 2 Nullable paramerter."));
                            //method.Comments.Add(new CodeCommentStatement("If there are more than 2 paramerter then add rest of the code."));
                            //method.Comments.Add(new CodeCommentStatement("If the code has been changed then to avoid overwritten your code, move this method implementation to partial class" 
                            //    + " and use following 2 properties to exclude this method from code regeneration."));
                            ////method.Comments.Add(new CodeCommentStatement("If code moved to partial class then use following 2 properties to exclude this method from code regeneration."));
                            //method.Comments.Add(new CodeCommentStatement("Configuration.RepositoryClass.MethodExcludeFilter"));
                            //method.Comments.Add(new CodeCommentStatement("Configuration.RepositoryClass.IsMethodExcludeFilterApplyToInterface"));

                            //arrayTypeMethodStatements += RepositoryNullableParamsCode(contextPrivateMemberName, m.Value.ContextProtpertyName
                            //    , m.Value.ParameterMappings);
                            tabs = $"\r{dicTabs["tab4"]}";
                            arrayTypeMethodStatements = $"var res = this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}"
                            + $"{ tabs}.AsNoTracking(){tabs}";
                            arrayTypeMethodStatements += $".WhereWithDistinctBy(conditionalArguments, " +
                                $"this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}, ','){tabs}" +
                                $".Select(selectionFields){tabs}" +
                                $".Pagination(conditionalArguments){tabs}" +                                
                                $".ToList();\r\r{dicTabs["tab3"]}";
                            arrayTypeMethodStatements +=
                                $"return Task.FromResult<IEnumerable<{m.Value.ReturnTypeBaseEntityFullName}>>(res)";

                            //arrayTypeMethodStatements += "\r" + dicTabs["tab3"] + "return Task.FromResult<" + returnTypePart
                            //        + ">(results)";
                            returnStatment = arrayTypeMethodStatements;
                            //create a function and send m.Value.ParameterMappings to it and then write code there.
                        }

                        else if (!m.Value.IsArray && m.Value.IsGroupBy)
                        {
                            tabs = $"\r{dicTabs["tab4"]}";
                            arrayTypeMethodStatements = $"var res = this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}"
                            + $"{tabs}.AsNoTracking(){tabs}";
                            arrayTypeMethodStatements += $".Where(conditionalArguments) {tabs}" +
                                $".GroupBy(conditionalArguments){tabs}" +
                                $".Count();\r\r{dicTabs["tab3"]}";
                            arrayTypeMethodStatements +=
                                $"return Task.FromResult(res)";

                            //arrayTypeMethodStatements += "\r" + dicTabs["tab3"] + "return Task.FromResult<" + returnTypePart
                            //        + ">(results)";
                            returnStatment = arrayTypeMethodStatements;
                            //create a function and send m.Value.ParameterMappings to it and then write code there.
                        }

                        else if (m.Value.IsArray && m.Value.IsGroupBy)
                        {
                            tabs = $"\r{dicTabs["tab4"]}";
                            arrayTypeMethodStatements = $"var res = this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}"
                            + $"{tabs}.AsNoTracking(){tabs}";
                            arrayTypeMethodStatements += 
                                $".GetListOfGroupValuePair(this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}, conditionalArguments) {tabs}" +                                
                                $".AsEnumerable();\r\r{dicTabs["tab3"]}";
                            arrayTypeMethodStatements +=
                                $"return Task.FromResult(res)";

                            returnStatment = arrayTypeMethodStatements;
                        }

                    }


                    method.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ">\r" + dicTabs["tab3"]);
                    methodInterface.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ">\r" + dicTabs["tab3"]);

                    method.Statements.Add(new CodeSnippetExpression(returnStatment));

                    int methodExcludeFilterContains = 0;
                    if (Configuration.RepositoryClass.MethodExcludeFilter != null)
                    {
                        methodExcludeFilterContains = Configuration.RepositoryClass.MethodExcludeFilter
                                                      .Where(x => x == "Get" + m.Key).Count();
                    }
                    if (methodExcludeFilterContains < 1)
                    {
                        repositoryClass.Members.Add(method);
                        repositoryInterface.Members.Add(methodInterface);
                    }
                    else if (!Configuration.RepositoryClass.IsMethodExcludeFilterApplyToInterface)
                    {
                        repositoryInterface.Members.Add(methodInterface);
                    }
                } //End of All methods foreach.

                repositoryNamespace.Types.Add(repositoryClass);
                targetUnit.Namespaces.Clear();
                targetUnit.Namespaces.Add(repositoryNamespace);
                GenerateCSharpCode(repositoryClassName + fileExtension, outputpath);
            }
            catch (Exception ex)
            {
                var logElement = generatedFilesLog[repositoryClassName + fileExtension];
                logElement.exception = ex;
                logElement.isException = true;
            }
            try
            {
                generatedFilesLog.Add(repositoryInterfaceName + fileExtension, new LogElement());
                repositoryNamespace.Types.Clear();
                repositoryNamespace.Types.Add(repositoryInterface);
                targetUnit.Namespaces.Clear();
                targetUnit.Namespaces.Add(repositoryNamespace);
                GenerateCSharpCode(repositoryInterfaceName + fileExtension, outputpath);
            }
            catch (Exception ex)
            {
                var logElementInterface = generatedFilesLog[repositoryInterfaceName + fileExtension];
                logElementInterface.exception = ex;
                logElementInterface.isException = true;
            }
        }

        private static string RepositoryNullableParamsCode(string contextPrivateMemberName, string contextPropretyName
           , List<QueryParameterMapping> parameterMappings)
        {
            string strResultCode = "\r" + dicTabs["tab3"];
            string strBoolLocalVariables = "";
            string AllIfPart = "if (";
            string AllResultPart = "results = this." + contextPrivateMemberName + "." + contextPropretyName
                + "\r" + dicTabs["tab5"] + ".Where(x => ";
            string ElsePart = "else\r" + dicTabs["tab3"] + "{\r" + dicTabs["tab4"]
                         + "results = this." + contextPrivateMemberName + "." + contextPropretyName + "\r" + dicTabs["tab5"]
                        + ".ToList();\r" + dicTabs["tab3"] + "}\r";
            string ElseIfResultPart = "";
            string ElseIfMultipleIfPart, ElseIfMultipleResultPart, ElseIfMultipleTotal = "";
            int index = 0;

            if (parameterMappings != null && parameterMappings.Count > 0)
            {
                foreach (var p in parameterMappings)
                {
                    strBoolLocalVariables += p.CSharpParameterType.FullName.ToLower().Contains("boolean")
                        ? "var b" + p.ParameterName + " = Convert.ToBoolean(" + p.ParameterName + ");\r" + dicTabs["tab3"] : "";
                    AllIfPart += p.ParameterName + " != null " +
                        (p.CSharpParameterType.FullName.ToLower().Contains("boolean") ? "" : "&& " + p.ParameterName + ".Value > 0")
                        + ((index < parameterMappings.Count - 1 && parameterMappings.Count > 1) ? " && "
                        : (index == parameterMappings.Count - 1) ? ")\r" + dicTabs["tab3"] + "{\r" + dicTabs["tab4"] : "");
                    AllResultPart += "x." + p.ParameterName + " == "
                        + (p.CSharpParameterType.FullName.ToLower().Contains("boolean") ? "b" : "") + p.ParameterName
                         + ((index < parameterMappings.Count - 1 && parameterMappings.Count > 1) ? " && "
                        : (index == parameterMappings.Count - 1) ? ")\r" + dicTabs["tab5"] + ".ToList();\r" + dicTabs["tab3"] + "}\r"
                        + dicTabs["tab3"] : "");
                    ElseIfResultPart += "else if (" + p.ParameterName + " != null " +
                        (p.CSharpParameterType.FullName.ToLower().Contains("boolean") ? "" : "&& " + p.ParameterName + ".Value > 0") +
                        ")\r"
                        + dicTabs["tab3"] + "{\r" + dicTabs["tab4"] +
                        "results = this." + contextPrivateMemberName + "." + contextPropretyName + "\r" + dicTabs["tab5"]
                        + ".Where(x => x." + p.ParameterName + " == "
                        + (p.CSharpParameterType.FullName.ToLower().Contains("boolean") ? "b" : "") + p.ParameterName
                        + ")\r" + dicTabs["tab5"] + ".ToList();\r"
                        + dicTabs["tab3"] + "}\r" + dicTabs["tab3"];
                    index++;
                }
                int[] arrayOfIndicesOfParams = Utility.getArrayOfIndicesOfList(parameterMappings.Count);
                for (int i = parameterMappings.Count - 1; i > 1; i--)
                {
                    Utility.listOfArray.Clear();
                    Utility.getCombination(arrayOfIndicesOfParams, arrayOfIndicesOfParams.Length, i);
                    foreach (var data in Utility.listOfArray)
                    {
                        ElseIfMultipleIfPart = "else if(";
                        ElseIfMultipleResultPart = "results = this." + contextPrivateMemberName + "." + contextPropretyName
                            + "\r" + dicTabs["tab5"] + ".Where(x => ";
                        for (int j = 0; j < data.Length; j++)
                        {
                            ElseIfMultipleIfPart += parameterMappings[data[j]].ParameterName + " != null " +
                                (parameterMappings[data[j]].CSharpParameterType.FullName.ToLower().Contains("boolean") ? ""
                                : "&& " + parameterMappings[data[j]].ParameterName + ".Value > 0")

                                + ((j < data.Length - 1 && data.Length > 1) ? " && "
                                : (j == data.Length - 1) ? ")\r" + dicTabs["tab3"] + "{\r" + dicTabs["tab4"] : "");
                            ElseIfMultipleResultPart += "x." + parameterMappings[data[j]].ParameterName + " == "
                                + (parameterMappings[data[j]].CSharpParameterType.FullName.ToLower().Contains("boolean") ? "b"
                                : "") + parameterMappings[data[j]].ParameterName
                                + ((j < data.Length - 1 && data.Length > 1) ? " && "
                                : (j == data.Length - 1) ? ")\r" + dicTabs["tab5"] + ".ToList();\r" + dicTabs["tab3"] + "}\r"
                                + dicTabs["tab3"] : "");
                        }
                        ElseIfMultipleTotal += ElseIfMultipleIfPart + ElseIfMultipleResultPart;
                    }
                }


                strResultCode += strBoolLocalVariables + AllIfPart + AllResultPart + ElseIfMultipleTotal + (parameterMappings.Count > 1 ? ElseIfResultPart : "") + ElsePart;
            }

            return strResultCode;
        }

        private static KeyValuePair<PropertyInfo, ForeignKeyAttribute> getFKPropert(string EntityFullName, string FieldName)
        {
            KeyValuePair<PropertyInfo, ForeignKeyAttribute> FKProperty = default;
            var type = assembly.GetType(EntityFullName);
            if (type != null)
            {
                FKProperty = type.GetProperties()
                    .Where(x => x.Name == FieldName && x.GetCustomAttribute<ForeignKeyAttribute>() != null)
                .Select(f => new { property = f, fkAttribute = f.GetCustomAttribute<ForeignKeyAttribute>() })
                .ToDictionary(x=> x.property, x=> x.fkAttribute)
                .FirstOrDefault();
            }

            return FKProperty;
        }

        private static string getPkOrFkFieldName(string EntityFullName, string EntityName, PropertyInfo fieldProperty)
        {
            string IdFieldName = string.Empty;
            Type fieldBaseEntityType = null;

            if (fieldProperty.PropertyType.IsGenericType)
            {
                fieldBaseEntityType = fieldProperty.PropertyType.GenericTypeArguments[0];

                IdFieldName = fieldBaseEntityType.GetProperties()
                    .Where(x => x.PropertyType.FullName == EntityFullName)
                    .Select(f => f.GetCustomAttribute<ForeignKeyAttribute>().Name)
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(IdFieldName))
                    IdFieldName = EntityName + "Id";
            }
            else
            {
                IdFieldName = getPKFields(fieldProperty.PropertyType.GetProperties())
                    .Select(x => x.Name)
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(IdFieldName))
                    IdFieldName = getEntityIdFieldName(EntityName, fieldProperty.PropertyType.GetProperties(),
                        out PropertyInfo IdFieldProperty);
            }

            return IdFieldName;
        }

        private static Dictionary<PropertyInfo, ForeignKeyAttribute> getFKFields(PropertyInfo[] properties)
        {
            return properties
                .Where(x => x.GetCustomAttribute<ForeignKeyAttribute>() != null)
                .Select(f => new { property = f, fkAttribute = f.GetCustomAttribute<ForeignKeyAttribute>() })
                .ToDictionary(z => z.property, z => z.fkAttribute);
        }
        private static List<PropertyInfo> getPKFields(PropertyInfo[] properties)
        {
            return properties
                .Where(x => x.GetCustomAttribute<KeyAttribute>() != null)
                .ToList();
        }
        private static void getPrimaryAndForiegnKeyProperties(PropertyInfo[] properties, 
            out List<PropertyInfo> pkFields, out Dictionary<PropertyInfo, ForeignKeyAttribute> fkFields)
        {
            pkFields = getPKFields(properties);
            fkFields = getFKFields(properties);
        }
        private static string getEntityIdFieldName(string EntityName, PropertyInfo[] properties, out PropertyInfo IdFieldPropperty)
        {
            IdFieldPropperty = properties.Where(x => (x != null && !string.IsNullOrEmpty(x.Name))
               && (x.Name.ToLower() == "id" || x.Name.ToLower() == EntityName.ToLower() + "id")).FirstOrDefault();
            if (IdFieldPropperty != null)
            {
                if (!dicEntitiesIdFieldName.ContainsKey(EntityName))
                    dicEntitiesIdFieldName.Add(EntityName, IdFieldPropperty.Name);
                else
                    dicEntitiesIdFieldName[EntityName] = IdFieldPropperty.Name;
                return IdFieldPropperty.Name;
            }
            else
                return string.Empty;
        }

        private static string getPluralizedValue(string input)
        {
            string output;

            if (Configuration.PluralizationFilter != null && Configuration.PluralizationFilter.Count > 0
                && Configuration.PluralizationFilter.ContainsKey(input))
            {
                output = Configuration.PluralizationFilter[input];
            }
            else
            {
                output = pluralizationService.Pluralize(input);
            }

            return output;
        }
        private static CodeNamespace addCopyRightComments(CodeNamespace ns, bool addAuthor = true, bool addCopyRight = false)
        {
            ns.Comments.Add(new CodeCommentStatement(PackageName.Replace("Dynamic.", "") + "\tv" + PackageVersion));
            //"Generated by GraphQL.Code.Generator.Core"));
            if (addCopyRight)
                ns.Comments.Add(new CodeCommentStatement("Copyright:                Nav Malik"));
            if (addAuthor)
                ns.Comments.Add(new CodeCommentStatement("Author:\t\t\t\t\tNav Malik"));
            return ns;
        }
        private static string addClassPrivateMember(string member, ref CodeTypeDeclaration _targetClass)
        {
            if (!string.IsNullOrEmpty(member))
            {
                var typeAndVariable = member.Trim().Split(' ');
                var variableName = string.Empty;
                if (typeAndVariable.Length == 2)
                {
                    AddField(typeAndVariable[1], typeAndVariable[0], MemberAttributes.Private, ref _targetClass);
                    variableName = typeAndVariable[1];
                }
                return variableName;
            }
            else
                return "member is empty";
        }
        private static void GenerateCSharpCode(string fileName, string outputPath)
        {
            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
            CodeGeneratorOptions options = new CodeGeneratorOptions();
            options.BracingStyle = "C";
            using (StreamWriter sourceWriter = new StreamWriter(outputPath + "\\" + fileName))
            {
                provider.GenerateCodeFromCompileUnit(
                    targetUnit, sourceWriter, options);
                generatedFilesCount++;
            }
        }
        public static int GenerateGraphQLCode()
        {
            dicTabs.Add("tab1", "\t");
            dicTabs.Add("tab2", "\t\t");
            dicTabs.Add("tab3", "\t\t\t");
            dicTabs.Add("tab4", "\t\t\t\t");
            dicTabs.Add("tab5", "\t\t\t\t\t");
            dicTabs.Add("tab6", "\t\t\t\t\t\t");
            dicTabs.Add("tab7", "\t\t\t\t\t\t\t");
            pluralizationService = new Pluralizer();
            dicEntitiesIdFieldName = new Dictionary<string, string>();

            var assembly = Assembly.GetExecutingAssembly();
            PackageName = assembly.GetName().Name;
            PackageVersion = assembly.GetName().Version.ToString();

            generatedFilesLog = new Dictionary<string, LogElement>();

            targetUnit = new CodeCompileUnit();
            if (Configuration.ElementsToGenerate.HasFlag(Configuration.Elements.Types))
            {
                Exception typeClassesException = null;
                try
                {

                    dicQueryFieldNamesAndParamsListWithTypes = new Dictionary<string, QueryRepositoryMethodMapping>();
                    dicRepositoryMethodNamesAndParamsListWithTypes = new Dictionary<string, List<QueryParameterMapping>>();
                    dicRepositoryMethodsForLoader = new Dictionary<string, LoaderRepositoryMapping>();
                    CreateTypeClasses(Configuration.InputDllNameAndPath, Configuration.TypeClasses.TypeClassesNamespace);
                    if (Configuration.ElementsToGenerate.HasFlag(Configuration.Elements.Repositoy))
                        CreateRepositoryClass(Configuration.RepositoryClass.RepositoryClassesNamespace);
                    if (Configuration.ElementsToGenerate.HasFlag(Configuration.Elements.Query))
                        CreateQueryClass();
                }
                catch (Exception ex)
                {
                    typeClassesException = ex;
                }

                string outputFileNameAndPath = Configuration.TypeClasses.Outputpath
                    + "\\Logs\\Log-GraphQL_Code_Generator_" + DateTime.Now.ToString("MM-dd-yyyy hh-mm-ss") + ".txt";
                if (!System.IO.Directory.Exists(Configuration.TypeClasses.Outputpath + "\\Logs"))
                {
                    System.IO.Directory.CreateDirectory(Configuration.TypeClasses.Outputpath + "\\Logs");
                }
                System.IO.FileInfo fileInfo = new FileInfo(outputFileNameAndPath);
                if (!fileInfo.Exists)
                {
                    var fs = fileInfo.Create();
                    fs.Close();
                }

                using (StreamWriter sw = new StreamWriter(outputFileNameAndPath))
                {
                    if (typeClassesException != null)
                    {
                        sw.WriteLine("GraphQL Type classes Code Generator failed with following error.");
                        sw.WriteLine();
                        sw.WriteLine("ERROR Message: \t\t" + typeClassesException.Message);
                        //sw.WriteLine("Stack Trace: " + typeClassesException.StackTrace);
                        //sw.WriteLine("Source: " + typeClassesException.Source);
                    }
                    else
                    {
                        sw.WriteLine("GraphQL Code Generation created following files.");
                        sw.WriteLine();
                        foreach (var typeLog in generatedFilesLog.Where(x => !x.Value.isException))
                        {
                            sw.WriteLine(typeLog.Key + "\t\t" + typeLog.Value.message);
                        }

                        sw.WriteLine();
                        sw.WriteLine();
                        sw.WriteLine("GraphQL Code Generation FAILED to created following files.");
                        sw.WriteLine();

                        foreach (var typeLog in generatedFilesLog.Where(x => x.Value.isException))
                        {
                            sw.WriteLine(typeLog.Key);// + "\t\t" + typeLog.Value.exception.Message);
                            sw.WriteLine("ERROR Message: \t\t" + typeLog.Value.exception.Message);
                            //sw.WriteLine("Stack Trace: " + typeLog.Value.exception.StackTrace);
                            //sw.WriteLine("Source: " + typeLog.Value.exception.Source);
                            sw.WriteLine();
                        }
                    }
                }
            }
            clearObjects();
            return generatedFilesCount;
        }
        private static void clearObjects()
        {
            dicRepositoryMethodNamesAndParamsListWithTypes = null;
            dicQueryFieldNamesAndParamsListWithTypes = null;
            generatedFilesLog = null;
            targetUnit = null;
            typeClass = null;
            queryClass = null;
            repositoryClass = null;
            pluralizationService = null;
            dicEntitiesIdFieldName = null;
            dicRepositoryMethodsForLoader = null;
        }
    }
}
