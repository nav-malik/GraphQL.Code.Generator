using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GraphQL.Code.Generator
{
    public static partial class GraphQLCodeGenerator
    {
        private static readonly string[] defaulAdditionalNamespacesForGraphQLMutation = { "System",
            "GraphQL.Types", "GraphQL.Extension.Types.Filter"};

        private static readonly string[] defaulAdditionalNamespacesForGraphQLInputTypes = { "GraphQL.Types" };

        private static readonly string[] defaulAdditionalNamespacesForMutationRepository = { "System", "System.Collections.Generic", "System.Linq",
            "System.Threading", "System.Threading.Tasks", "System.Reflection",
            Configuration.ORMType == Configuration.ORMTypes.EF6 ? "System.Data.Entity"
                : Configuration.ORMType == Configuration.ORMTypes.EFCore ? "Microsoft.EntityFrameworkCore" : "",
            "Linq.Extension", "Linq.Extension.Filter"};
        private class MutationInputTypeFieldMapping
        {
            public PropertyInfo Property;
            public RequiredAttribute Required;
            public KeyAttribute Key;
            public bool IsNullable;
        }

        private static Dictionary<string, MutationInputTypeFieldMapping> dicMutationInputTypeFields;

        private class StoredProcedureInfo
        {
            public string StoredProcedureName;
            public List<ParameterInfo> Parameters;
            public bool IsReturnTypeScalar = true;
            public string ReturnTypeName;
        }

        private class MutationTypeRepositoryMapping
        {
            public string InputTypeBaseEntityName;
            public string InputTypeBaseEntityFullName;
            public string GraphInputTypeName;
            public Dictionary<string, MutationInputTypeFieldMapping> dicMutationInputTypeFieldMapping;
            public List<string> KeyPropertyNames;
            public string ContextProtpertyName;
            public string SingularizeName;
            public bool IsStoredProcedure = false;
            public StoredProcedureInfo StoredProcedureInfo;

            public List<MutationFieldAndRepositoryMethodMapping> MutationFieldAndRepositoryMethodMappings;
        }

        private static Dictionary<string, MutationTypeRepositoryMapping> dicMutationTypeRepositoryMapping;

        private class MutationFieldParameterMapping
        {
            public string ParameterName;
            /// <summary>
            /// Enter GraphQL Scalar Type name like, "IntGraphType".
            /// </summary>
            public string GraphQLParameterType;

            public string RepositoryParameterName;

            /// <summary>
            /// Enter corresponding C# type name, like include namespaces.
            /// </summary>
            public string CSharpParameterTypeName;

            /// <summary>
            /// Enter corresponding C# type full name, like include namespaces.
            /// </summary>
            public string CSharpParameterTypeFullName;

            /// <summary>
            /// Default value is true, so can be left unset if param is nullable.
            /// </summary>
            public bool IsNullable = true;
            public bool IsArray = false;
            public bool IsArrayRequired = false;
            public bool IsKeyParam = false;
        }

        private class MutationFieldAndRepositoryMethodMapping
        {
            public string MutationFieldName;
            public List<MutationFieldParameterMapping> MappingParameters;
            public string MutationFieldReturnType;
            public bool IsFieldReturnTypeArray = false;

            public string RepositoryMethodName;
            public string RepositoryMethodReturnTypeFullName;
        }

        private static void AddStoredProceduresAsMutations(string assemblyNameAndPathAndExtension)
        {
            assembly = Assembly.LoadFrom(assemblyNameAndPathAndExtension);
            var types = assembly.GetTypes()
                .Where(t =>
                    (
                        (Configuration.StoredProcedureAsMutation.IsDbContextBaseTypeNullAllowed && t.BaseType == null)
                        || (!Configuration.StoredProcedureAsMutation.IsDbContextBaseTypeNullAllowed && t.BaseType != null
                            && (
                                (Configuration.StoredProcedureAsMutation.DdContextBaseClassInclude != null
                                   && Configuration.StoredProcedureAsMutation.DdContextBaseClassInclude.IsMatch(t.BaseType.Name)
                                   )
                                || Configuration.StoredProcedureAsMutation.DdContextBaseClassInclude == null
                                )
                            && (
                                (Configuration.StoredProcedureAsMutation.DdContextBaseClassExclude != null
                                && !Configuration.StoredProcedureAsMutation.DdContextBaseClassExclude.IsMatch(t.BaseType.Name)
                                )
                                || Configuration.StoredProcedureAsMutation.DdContextBaseClassExclude == null
                               )
                             )
                    )
                    &&
                    (
                        (
                            (Configuration.StoredProcedureAsMutation.DdContextClassNameInclude != null
                                && Configuration.StoredProcedureAsMutation.DdContextClassNameInclude.IsMatch(t.Name)
                                )
                            || Configuration.StoredProcedureAsMutation.DdContextClassNameInclude == null
                        )
                        &&
                        (
                            (Configuration.StoredProcedureAsMutation.DdContextClassNameExclude != null
                                && !Configuration.StoredProcedureAsMutation.DdContextClassNameExclude.IsMatch(t.Name)
                                )
                            || Configuration.StoredProcedureAsMutation.DdContextClassNameExclude == null
                        )
                    )
                ).ToList();

            foreach (var tc in types)
            {
                try
                {
                    if (!generatedFilesLog.ContainsKey(tc.Name))
                    {
                        var logElement = new LogElement();
                        generatedFilesLog.Add(tc.Name, logElement);
                    }

                    var methods = tc.GetMethods()
                        .Where(m =>
                            ( // Method Regex for Include Exclude Section
                                (
                                    (Configuration.StoredProcedureAsMutation.MethodInclude != null
                                        && Configuration.StoredProcedureAsMutation.MethodInclude.IsMatch(m.Name)
                                        )
                                    || Configuration.StoredProcedureAsMutation.MethodInclude == null
                                )
                                &&
                                (
                                    (Configuration.StoredProcedureAsMutation.MethodExclude != null
                                        && !Configuration.StoredProcedureAsMutation.MethodExclude.IsMatch(m.Name)
                                        )
                                    || Configuration.StoredProcedureAsMutation.MethodExclude == null
                                )
                            )
                            &&
                            ( // Method Last Param Regex Section
                                (
                                    Configuration.StoredProcedureAsMutation.IgnoreMethodsWithLastOutParam
                                    && !m.GetParameters().Last().IsOut
                                    &&
                                    (
                                        (Configuration.StoredProcedureAsMutation.LastOutParameterNameExclude != null
                                            && !Configuration.StoredProcedureAsMutation.LastOutParameterNameExclude
                                                .IsMatch(m.GetParameters().Last().Name)
                                            )
                                        || Configuration.StoredProcedureAsMutation.LastOutParameterNameExclude == null
                                    )
                                )
                                || !Configuration.StoredProcedureAsMutation.IgnoreMethodsWithLastOutParam
                            )
                            &&
                            ( // Method Return Type Regex section
                                Configuration.StoredProcedureAsMutation.IsAnyMethodReturnTypeAllowed
                                ||
                                (
                                    !Configuration.StoredProcedureAsMutation.IsAnyMethodReturnTypeAllowed
                                    &&
                                    (
                                        (Configuration.StoredProcedureAsMutation.IsMethodReturnTypeBaseTypeNullAllowed
                                        && (m.ReturnType == null || m.ReturnType.BaseType == null)
                                        )
                                        ||
                                        (
                                            !Configuration.StoredProcedureAsMutation.IsMethodReturnTypeBaseTypeNullAllowed
                                            && m.ReturnType != null && m.ReturnType.BaseType != null
                                            &&
                                            (
                                                (Configuration.StoredProcedureAsMutation.MethodReturnTypeBaseClassInclude != null
                                                    && Configuration.StoredProcedureAsMutation.MethodReturnTypeBaseClassInclude
                                                        .IsMatch(m.ReturnType.BaseType.Name)
                                                    )
                                                || Configuration.StoredProcedureAsMutation.MethodReturnTypeBaseClassInclude == null
                                            )
                                        )
                                    )
                                )
                            )
                        ).ToList();
                    var mutationTypeRepositoryMapping = new MutationTypeRepositoryMapping
                    {
                        IsStoredProcedure = true,
                        InputTypeBaseEntityName = tc.Name,
                        InputTypeBaseEntityFullName = tc.FullName,
                        MutationFieldAndRepositoryMethodMappings = new List<MutationFieldAndRepositoryMethodMapping>()
                    };
                    Regex nullableParam = new Regex(pattern: @"^.*nullable.*$", RegexOptions.IgnoreCase);
                    foreach (var m in methods)
                    {
                        try
                        {
                            if (!generatedFilesLog.ContainsKey(tc.Name + " => " + m.Name))
                            {
                                var logElement = new LogElement();
                                generatedFilesLog.Add(tc.Name + " => " + m.Name, logElement);
                            }

                            var parameters = m.GetParameters();
                            var mappingParameters = new List<MutationFieldParameterMapping>();
                            foreach (var p in parameters)
                            {
                                mappingParameters.Add(new MutationFieldParameterMapping
                                {
                                    ParameterName = p.Name,
                                    GraphQLParameterType = getGraphQLTypeFromDotNetType(p.ParameterType.GetUnderlyingType().Name),
                                    RepositoryParameterName = p.Name,
                                    CSharpParameterTypeName = p.ParameterType.GetUnderlyingType().Name
                                        + (nullableParam.IsMatch(p.ParameterType.FullName) ? "?" : ""),
                                    CSharpParameterTypeFullName = p.ParameterType.GetUnderlyingType().FullName
                                        + (nullableParam.IsMatch(p.ParameterType.FullName) ? "?" : ""),
                                    IsNullable = nullableParam.IsMatch(p.ParameterType.FullName)
                                });
                            }

                            mutationTypeRepositoryMapping.MutationFieldAndRepositoryMethodMappings
                                .Add(new MutationFieldAndRepositoryMethodMapping
                                {
                                    MutationFieldName = Utility.getCamelCaseString(m.Name),
                                    MutationFieldReturnType = getGraphQLTypeFromDotNetType(m.ReturnType.Name),
                                    RepositoryMethodName = m.Name,// Utility.getTitleCaseString(m.Name),
                                    RepositoryMethodReturnTypeFullName = m.ReturnType.Name,
                                    MappingParameters = mappingParameters,
                                });

                        }
                        catch (Exception e)
                        {
                            var logEl = generatedFilesLog[tc.Name + " => " + m.Name];
                            logEl.isException = true;
                            logEl.exception = e;
                        }
                    }

                    dicMutationTypeRepositoryMapping.Add(tc.Name, mutationTypeRepositoryMapping);
                }
                catch (Exception ex)
                {
                    var logEl = generatedFilesLog[tc.Name];
                    logEl.isException = true;
                    logEl.exception = ex;
                }
            }
        }

        private static void AddMutationInputTypeConstructor(string typeBaseEntityName, string typeBaseEntityFullName
            , MutationTypeRepositoryMapping mutationTypeRepositoryMapping, string prefix, bool isAllFieldsNullable)
        //, string GraphQLGeneratedTypesClassNamePostfix, PropertyInfo[] properties, FieldInfo[] fields)
        {
            // Declare the constructor
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes =
                MemberAttributes.Public | MemberAttributes.Final;

            constructor.Statements.Add(new CodeSnippetStatement(""));

            constructor.Statements.Add(new CodeSnippetExpression("Name = \"" + prefix + typeBaseEntityName
                + Configuration.MutationInputTypeClasses.NameSuffix + "\""));
            constructor.Statements.Add(new CodeSnippetStatement(""));

            string nullableString = string.Empty;

            bool isMultiKey = mutationTypeRepositoryMapping
                .KeyPropertyNames.Count > 1;

            foreach (var prop in mutationTypeRepositoryMapping.dicMutationInputTypeFieldMapping)
            {
                if (isAllFieldsNullable)
                {
                    nullableString = ", nullable: true";
                }
                else
                {
                    if ((prop.Value.Required != null && prop.Value.Key == null)
                        || (prop.Value.Required != null && prop.Value.Key != null && isMultiKey))
                        nullableString = string.Empty;
                    else if ((prop.Value.Required != null && prop.Value.Key != null && !isMultiKey)
                        || (prop.Value.Required == null && prop.Value.Key == null)
                        || (prop.Value.IsNullable))
                        nullableString = ", nullable: true";
                }
                constructor.Statements.Add(new CodeSnippetExpression("Field(x => x." + prop.Key + nullableString + ")"));
            }
            constructor.Statements.Add(new CodeSnippetStatement(""));

            typeClass.Members.Add(constructor);
        }
        private static void CreateMutationInputTypeClasses(string assemblyNameAndPathAndExtension, string typesNamespaceValue)
        {
            string classPostfix, fileExtension, outputpath;
            classPostfix = Configuration.MutationInputTypeClasses.ClassSuffix;
            fileExtension = Configuration.MutationInputTypeClasses.FileExtension;
            outputpath = Configuration.MutationInputTypeClasses.Outputpath;

            PropertyInfo[] props;

            DirectoryInfo dInfo = new DirectoryInfo(outputpath);
            if (!dInfo.Exists)
            {
                Directory.CreateDirectory(outputpath);
            }

            CodeNamespace typesNamespace = new CodeNamespace(typesNamespaceValue);
            foreach (string ns in defaulAdditionalNamespacesForGraphQLInputTypes)
            {
                typesNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
            }

            if (!string.IsNullOrEmpty(Configuration.MutationInputTypeClasses.AdditionalNamespaces))
            {
                var additionalNamespaces = Configuration.MutationInputTypeClasses.AdditionalNamespaces.Split(',');
                foreach (var ns in additionalNamespaces)
                {
                    typesNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }
            }

            dicMutationTypeRepositoryMapping = new Dictionary<string, MutationTypeRepositoryMapping>();

            assembly = Assembly.LoadFrom(assemblyNameAndPathAndExtension);
            var types = assembly.GetTypes()
                .Where(x => !x.IsInterface && !string.IsNullOrEmpty(x.Name) && !string.IsNullOrEmpty(x.Namespace) &&
                    (
                        (
                            (Configuration.MutationInputTypeClasses.EntityClassesNamespacesInclude != null
                            && Configuration.MutationInputTypeClasses.EntityClassesNamespacesInclude.IsMatch(x.Namespace)
                            )
                            || Configuration.MutationInputTypeClasses.EntityClassesNamespacesInclude == null
                        )
                        &&
                        (
                            (Configuration.MutationInputTypeClasses.EntityClassesNamespacesExclude != null
                            && !Configuration.MutationInputTypeClasses.EntityClassesNamespacesExclude.IsMatch(x.Namespace)
                            )
                            || Configuration.MutationInputTypeClasses.EntityClassesNamespacesExclude == null
                        )
                        &&
                        (
                            (Configuration.MutationInputTypeClasses.EntityClassNamesInclude != null
                            && Configuration.MutationInputTypeClasses.EntityClassNamesInclude.IsMatch(x.Name)
                            )
                            || Configuration.MutationInputTypeClasses.EntityClassNamesInclude == null
                        )
                        &&
                        (
                            (Configuration.MutationInputTypeClasses.EntityClassNamesExclude != null
                            && !Configuration.MutationInputTypeClasses.EntityClassNamesExclude.IsMatch(x.Name)
                            )
                            || Configuration.MutationInputTypeClasses.EntityClassNamesExclude == null
                        )
                        &&
                        (
                            (Configuration.ViewNameFilter != null
                            && !Configuration.ViewNameFilter.IsMatch(x.Name)
                            )
                            || Configuration.ViewNameFilter == null
                        )
                        && !x.Name.ToLower().Contains("dbcontext")
                    )
                );

            string typeName = "", typeFullName = string.Empty, prefix = string.Empty;
            string completeTypeName = string.Empty;
            bool isAllFieldsNullable = false;
            MutationTypeRepositoryMapping mutationTypeRepositoryMapping = null;
            List<MutationFieldAndRepositoryMethodMapping> mutationFieldAndRepositoryMethodMappings = null;

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
                    typeFullName = tc.FullName.Replace('+', '.');

                    for (int i = 0; i < 2; i++)
                    {
                        if (i == 1)
                        {
                            isAllFieldsNullable = true;
                            prefix = "Update";
                        }
                        else
                        {
                            isAllFieldsNullable = false;
                            prefix = "Create";
                        }

                        completeTypeName = prefix + typeName + classPostfix;
                        if (!generatedFilesLog.ContainsKey(completeTypeName + fileExtension))
                        {
                            generatedFilesLog.Add(completeTypeName + fileExtension, new LogElement());
                        }
                        typesNamespace.Types.Clear();
                        if (typesNamespace.Comments.Count < 1)
                            typesNamespace = addCopyRightComments(typesNamespace);

                        typeClass = new CodeTypeDeclaration(completeTypeName);
                        typeClass.BaseTypes.Add("InputObjectGraphType<" + tc.FullName.Replace('+', '.') + ">");
                        typeClass.IsClass = true;
                        typeClass.IsPartial = true;
                        typeClass.TypeAttributes =
                            TypeAttributes.Public;

                        if (!isAllFieldsNullable)
                        {
                            props = tc.GetProperties()
                                .Where(prop => !((prop.PropertyType.IsClass || prop.PropertyType.IsInterface)
                                && (!prop.PropertyType.FullName.ToLower().Contains("system.") || prop.PropertyType.IsGenericType)))
                                .ToArray();

                            dicMutationInputTypeFields = props
                            .ToDictionary(n => n.Name, v => new MutationInputTypeFieldMapping
                            {
                                Property = v,
                                Key = v.GetCustomAttribute<KeyAttribute>(),
                                Required = v.GetCustomAttribute<RequiredAttribute>(),
                                IsNullable = v.PropertyType.FullName.ToLower().Contains("nullable")
                            });
                            mutationFieldAndRepositoryMethodMappings = new List<MutationFieldAndRepositoryMethodMapping>
                            {
                                new MutationFieldAndRepositoryMethodMapping
                                {
                                    MutationFieldName = "create" + typeName,
                                    IsFieldReturnTypeArray = false,
                                    MappingParameters = new List<MutationFieldParameterMapping>
                                    {
                                        new MutationFieldParameterMapping
                                        {
                                            ParameterName = Utility.getCamelCaseString(typeName),
                                            GraphQLParameterType = "Create" + typeName + classPostfix,
                                            RepositoryParameterName = Utility.getCamelCaseString(typeName),
                                            CSharpParameterTypeName = typeName,
                                            CSharpParameterTypeFullName = typeFullName,
                                            IsNullable = false,
                                            IsArray = false,
                                            IsArrayRequired = false,
                                        }
                                    },
                                    MutationFieldReturnType = typeName + Configuration.MutationInputTypeClasses.TypeClassSuffix,
                                    RepositoryMethodName = "Create" + typeName,
                                    RepositoryMethodReturnTypeFullName = typeFullName,
                                }
                            };

                            var firstKeyProp = dicMutationInputTypeFields
                                            .Where(f => f.Value.Key != null)
                                            .Select(f => f.Value.Property)
                                            .FirstOrDefault();
                            if (firstKeyProp != null)
                            {
                                mutationFieldAndRepositoryMethodMappings.Add(new MutationFieldAndRepositoryMethodMapping
                                {
                                    MutationFieldName = "update" + typeName + "By" + firstKeyProp.Name,
                                    IsFieldReturnTypeArray = false,
                                    MappingParameters = new List<MutationFieldParameterMapping>
                                    {
                                        new MutationFieldParameterMapping
                                        {
                                            ParameterName = Utility.getCamelCaseString(firstKeyProp.Name),
                                            GraphQLParameterType =  getGraphQLTypeFromDotNetType(firstKeyProp.PropertyType.Name),
                                            RepositoryParameterName = Utility.getCamelCaseString(firstKeyProp.Name),
                                            CSharpParameterTypeName = firstKeyProp.PropertyType.Name,
                                            CSharpParameterTypeFullName = firstKeyProp.PropertyType.FullName,
                                            IsNullable = false,
                                            IsArray = false,
                                            IsArrayRequired = false,
                                            IsKeyParam = true
                                        },
                                        new MutationFieldParameterMapping
                                        {
                                            ParameterName = Utility.getCamelCaseString(typeName),
                                            GraphQLParameterType = "Update" + typeName + classPostfix,
                                            RepositoryParameterName = Utility.getCamelCaseString(typeName),
                                            CSharpParameterTypeName = typeName,
                                            CSharpParameterTypeFullName = typeFullName,
                                            IsNullable = false,
                                            IsArray = false,
                                            IsArrayRequired = false,
                                        },
                                        new MutationFieldParameterMapping
                                        {
                                            ParameterName = "ignoreNullValues",
                                            GraphQLParameterType =  getGraphQLTypeFromDotNetType("bool"),
                                            RepositoryParameterName = "ignoreNullValues",
                                            CSharpParameterTypeName = "bool",
                                            CSharpParameterTypeFullName = "System.Boolean",
                                            IsNullable = true,
                                            IsArray = false,
                                            IsArrayRequired = false,
                                        }
                                    },
                                    MutationFieldReturnType = typeName + Configuration.MutationInputTypeClasses.TypeClassSuffix,
                                    RepositoryMethodName = "Update" + typeName + "By" + firstKeyProp.Name,
                                    RepositoryMethodReturnTypeFullName = typeFullName,
                                });
                            }

                            mutationFieldAndRepositoryMethodMappings.Add(new MutationFieldAndRepositoryMethodMapping
                            {
                                MutationFieldName = "update" + typeName,
                                IsFieldReturnTypeArray = false,
                                MappingParameters = new List<MutationFieldParameterMapping>
                                {
                                    new MutationFieldParameterMapping
                                    {
                                        ParameterName = "search",
                                        GraphQLParameterType = "SearchInputType",
                                        RepositoryParameterName = "conditionalArguments",
                                        CSharpParameterTypeName = "IDictionary<string, object>",
                                        CSharpParameterTypeFullName = "IDictionary<string, object>",
                                        IsNullable = true
                                    },
                                    new MutationFieldParameterMapping
                                    {
                                        ParameterName = Utility.getCamelCaseString(typeName),
                                        GraphQLParameterType =  "Update" + typeName + classPostfix,
                                        RepositoryParameterName = Utility.getCamelCaseString(typeName),
                                        CSharpParameterTypeName = typeName,
                                        CSharpParameterTypeFullName = typeFullName,
                                        IsNullable = false,
                                        IsArray = false,
                                        IsArrayRequired = false,
                                    },
                                    new MutationFieldParameterMapping
                                    {
                                        ParameterName = "ignoreNullValues",
                                        GraphQLParameterType =  getGraphQLTypeFromDotNetType("bool"),
                                        RepositoryParameterName = "ignoreNullValues",
                                        CSharpParameterTypeName = "bool",
                                        CSharpParameterTypeFullName = "System.Boolean",
                                        IsNullable = true,
                                        IsArray = false,
                                        IsArrayRequired = false,
                                    }
                                },
                                MutationFieldReturnType = typeName + Configuration.MutationInputTypeClasses.TypeClassSuffix,
                                RepositoryMethodName = "Update" + typeName,
                                RepositoryMethodReturnTypeFullName = typeFullName,
                            });

                            if (firstKeyProp != null)
                            {
                                mutationFieldAndRepositoryMethodMappings.Add(new MutationFieldAndRepositoryMethodMapping
                                {
                                    MutationFieldName = "delete" + typeName + "By" + firstKeyProp.Name,
                                    IsFieldReturnTypeArray = false,
                                    MappingParameters = new List<MutationFieldParameterMapping>
                                    {
                                        new MutationFieldParameterMapping
                                        {
                                            ParameterName = Utility.getCamelCaseString(firstKeyProp.Name),
                                            GraphQLParameterType =  getGraphQLTypeFromDotNetType(firstKeyProp.PropertyType.Name),
                                            RepositoryParameterName = Utility.getCamelCaseString(firstKeyProp.Name),
                                            CSharpParameterTypeName = firstKeyProp.PropertyType.Name,
                                            CSharpParameterTypeFullName = firstKeyProp.PropertyType.FullName,
                                            IsNullable = false,
                                            IsArray = false,
                                            IsArrayRequired = false,
                                            IsKeyParam = true
                                        }
                                    },
                                    MutationFieldReturnType = getGraphQLTypeFromDotNetType("int"),
                                    RepositoryMethodName = "Delete" + typeName + "By" + firstKeyProp.Name,
                                    RepositoryMethodReturnTypeFullName = "int",
                                });
                            }

                            mutationFieldAndRepositoryMethodMappings.Add(new MutationFieldAndRepositoryMethodMapping
                            {
                                MutationFieldName = "delete" + typeName,
                                IsFieldReturnTypeArray = false,
                                MappingParameters = new List<MutationFieldParameterMapping>
                                {
                                    new MutationFieldParameterMapping
                                    {
                                        ParameterName = "search",
                                        GraphQLParameterType = "SearchInputType",
                                        RepositoryParameterName = "conditionalArguments",
                                        CSharpParameterTypeName = "IDictionary<string, object>",
                                        CSharpParameterTypeFullName = "IDictionary<string, object>",
                                        IsNullable = true
                                    }
                                },
                                MutationFieldReturnType = getGraphQLTypeFromDotNetType("int"),
                                RepositoryMethodName = "Delete" + typeName,
                                RepositoryMethodReturnTypeFullName = "int",
                            });



                            mutationTypeRepositoryMapping = new MutationTypeRepositoryMapping
                            {
                                InputTypeBaseEntityName = typeName,
                                InputTypeBaseEntityFullName = typeFullName,
                                dicMutationInputTypeFieldMapping = dicMutationInputTypeFields,
                                GraphInputTypeName = typeName + classPostfix,
                                KeyPropertyNames = dicMutationInputTypeFields
                                            .Where(f => f.Value.Key != null)
                                            .Select(f => f.Key)
                                            .ToList(),
                                SingularizeName = pluralizationService.Singularize(typeName),
                                ContextProtpertyName = getPluralizedValue(typeName),
                                IsStoredProcedure = false,
                                StoredProcedureInfo = null,
                                MutationFieldAndRepositoryMethodMappings = mutationFieldAndRepositoryMethodMappings
                            };

                            dicMutationTypeRepositoryMapping.Add(typeName, mutationTypeRepositoryMapping);
                        }

                        AddMutationInputTypeConstructor(typeName, typeFullName
                            , mutationTypeRepositoryMapping, prefix, isAllFieldsNullable);

                        typesNamespace.Types.Add(typeClass);
                        targetUnit.Namespaces.Clear();
                        targetUnit.Namespaces.Add(typesNamespace);

                        if (Configuration.ElementsToGenerate.HasFlag(Configuration.Elements.MutationInputTypes))
                            GenerateCSharpCode(completeTypeName + fileExtension, outputpath);
                    }
                }
                catch (Exception ex)
                {
                    if (!generatedFilesLog.ContainsKey(typeName + classPostfix + fileExtension))
                    {
                        var logElement = new LogElement();
                        logElement.exception = ex;
                        logElement.isException = true;
                        generatedFilesLog.Add(typeName + classPostfix + fileExtension, logElement);
                    }
                    else
                    {
                        var logEl = generatedFilesLog[typeName + classPostfix + fileExtension];
                        logEl.isException = true;
                        logEl.exception = ex;
                    }
                }
            }
        }

        private static void AddMutationConstructor(IDictionary<string, string> parameters,
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

            constructor.Statements.Add(new CodeSnippetExpression("Name = nameof(" + mutationClass.Name + ")"));
            constructor.Statements.Add(new CodeSnippetStatement(""));


            string returnStatment = "", fieldName = "", fieldArguments = "", resolver = "", resolverReturn = "";
            int totalParams = -1, index = 0;
            bool isDeleteField = false, isStoredProcedure = false;

            foreach (var m in dicMutationTypeRepositoryMapping)
            {
                isStoredProcedure = m.Value.IsStoredProcedure;
                if (m.Value.MutationFieldAndRepositoryMethodMappings.Count > 0)
                {
                    foreach (var mr in m.Value.MutationFieldAndRepositoryMethodMappings)
                    {
                        isDeleteField = mr.MutationFieldName.StartsWith("delete", StringComparison.OrdinalIgnoreCase);
                        returnStatment = "Field<" + (mr.IsFieldReturnTypeArray ? "ListGraphType<" : "")
                            + mr.MutationFieldReturnType
                            + (mr.IsFieldReturnTypeArray ? ">>" : ">");
                        fieldName = mr.MutationFieldName;

                        resolver = ".ResolveAsync(async context => " + "\r" + dicTabs["tab3"] +
                            "{";
                        resolverReturn = "return await this." + repositoryPrivateMemberName + "." + mr.RepositoryMethodName + "(";

                        fieldArguments = "";
                        totalParams = mr.MappingParameters.Count;
                        index = 0;
                        foreach (var p in mr.MappingParameters)
                        {
                            index++;

                            fieldArguments += "\r" + dicTabs["tab3"] + ".Argument<" + (p.IsNullable ? p.GraphQLParameterType
                            : "NonNullGraphType<" + p.GraphQLParameterType + ">") + ">(\""
                            + Utility.getCamelCaseString(p.ParameterName) + "\")";

                            if (p.CSharpParameterTypeFullName == "IDictionary<string, object>")
                            {
                                resolver += "\r" + dicTabs["tab4"] +
                                    "Dictionary<string, object> args = new Dictionary<string, object>(); ";
                                resolver += "\r" + dicTabs["tab4"] +
                                    "object" + " " + Utility.getCamelCaseString(p.ParameterName)
                                    + " = context.GetArgument<" + "object" + ">(\""
                                    + Utility.getCamelCaseString(p.ParameterName) + "\");";
                                resolver += "\r" + dicTabs["tab4"] +
                                    "args.Add(\"" + Utility.getCamelCaseString(p.ParameterName) + "\", "
                                    + Utility.getCamelCaseString(p.ParameterName) + ");";
                                resolverReturn += "\r" + dicTabs["tab5"] + p.RepositoryParameterName + ": args";

                            }
                            else
                            {
                                resolver += "\r" + dicTabs["tab4"] +
                                    (p.CSharpParameterTypeFullName.ToLower().Contains("system.") ?
                                        p.CSharpParameterTypeName : p.CSharpParameterTypeFullName) + " " + Utility.getCamelCaseString(p.ParameterName)
                                    + " = context.GetArgument<" + (p.CSharpParameterTypeFullName.ToLower().Contains("system.") ?
                                        p.CSharpParameterTypeName : p.CSharpParameterTypeFullName) + ">(\""
                                    + Utility.getCamelCaseString(p.ParameterName) + "\");";

                                resolverReturn += "\r" + dicTabs["tab5"] + p.RepositoryParameterName + ": " +
                                    Utility.getCamelCaseString(p.ParameterName);

                            }

                            if (index < totalParams)
                                resolverReturn += ",";
                            else
                                resolverReturn += (!isDeleteField && !isStoredProcedure ? ",\r" + dicTabs["tab5"] + "selectionFields: context.SubFields.Keys" : "") + ");";
                        }
                        resolver += "\r" + dicTabs["tab4"] + resolverReturn;
                        resolver += "\r" + dicTabs["tab3"] + "})";
                        constructor.Statements.Add(new CodeSnippetExpression(returnStatment + "(\""
                            + fieldName + "\")" + fieldArguments + "\r" + dicTabs["tab3"] + resolver
                                ));
                        constructor.Statements.Add(new CodeSnippetStatement(""));
                    }
                }


            } //End of All methods foreach.

            mutationClass.Members.Add(constructor);
        }

        private static void CreateMutationClass()
        {
            string fileExtension, outputpath, mutationNamespaceValue, mutationClassName;
            fileExtension = Configuration.MutationClass.FileExtension;
            outputpath = Configuration.MutationClass.Outputpath;
            mutationNamespaceValue = Configuration.MutationClass.MutationClassNamespace;
            mutationClassName = Configuration.MutationClass.MutationClassName;

            CodeNamespace mutationNamespace = new CodeNamespace(mutationNamespaceValue);

            try
            {
                generatedFilesLog.Add(mutationClassName + fileExtension, new LogElement());
                DirectoryInfo dInfo = new DirectoryInfo(outputpath);
                if (!dInfo.Exists)
                {
                    Directory.CreateDirectory(outputpath);
                }

                foreach (string ns in defaulAdditionalNamespacesForGraphQLMutation)
                {
                    mutationNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }

                if (!string.IsNullOrEmpty(Configuration.MutationClass.AdditionalNamespaces))
                {
                    var additionalNamespaces = Configuration.MutationClass.AdditionalNamespaces.Split(',');
                    foreach (var ns in additionalNamespaces)
                    {
                        mutationNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                    }
                }
                if (!string.IsNullOrEmpty(Configuration.TypeClasses.TypeClassesNamespace))
                    mutationNamespace.Imports.Add(new CodeNamespaceImport(Configuration.TypeClasses.TypeClassesNamespace));
                if (!string.IsNullOrEmpty(Configuration.MutationInputTypeClasses.InputTypeClassesNamespace))
                    mutationNamespace.Imports.Add(new CodeNamespaceImport(Configuration.MutationInputTypeClasses.InputTypeClassesNamespace));
                if (!string.IsNullOrEmpty(Configuration.MutationRepositoryClass.RepositoryClassesNamespace))
                    mutationNamespace.Imports.Add(new CodeNamespaceImport(Configuration.MutationRepositoryClass.RepositoryClassesNamespace));
                if (mutationNamespace.Comments.Count < 1)
                    mutationNamespace = addCopyRightComments(mutationNamespace);
                mutationClass = new CodeTypeDeclaration(mutationClassName);
                mutationClass.BaseTypes.Add("ObjectGraphType");
                mutationClass.IsClass = true;
                mutationClass.IsPartial = true;
                mutationClass.TypeAttributes = TypeAttributes.Public;

                var repositoryPrivateMemberName = addClassPrivateMember(Configuration.MutationClass.RepositoryPrivateMember
                            , ref mutationClass);

                IList<string> parameterToFieldAssignmentStatments = new List<string>();
                IDictionary<string, string> constructorParameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(Configuration.MutationClass.RepositoryConstructorParameter))
                {
                    var typeAndVariableForRepositoryParameter =
                        Configuration.MutationClass.RepositoryConstructorParameter.Split(' ');
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
                AddMutationConstructor(constructorParameters, parameterToFieldAssignmentStatments, repositoryPrivateMemberName);


                mutationNamespace.Types.Add(mutationClass);
                targetUnit.Namespaces.Clear();
                targetUnit.Namespaces.Add(mutationNamespace);
                GenerateCSharpCode(mutationClassName + fileExtension, outputpath);
            }
            catch (Exception ex)
            {
                var logElement = generatedFilesLog[mutationClassName + fileExtension];
                logElement.exception = ex;
                logElement.isException = true;
            }
        }

        private static void AddMutationRepositoryConstructor(IDictionary<string, string> parameters,
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
        private static void CreateMutationRepositoryClass(string repositoryNamespaceValue)
        {
            string fileExtension, outputpath;
            fileExtension = Configuration.MutationRepositoryClass.FileExtension;
            outputpath = Configuration.MutationRepositoryClass.Outputpath;
            string repositoryClassName, repositoryInterfaceName;
            repositoryClassName = Configuration.MutationRepositoryClass.RepositoryClassName;
            repositoryInterfaceName = "I" + Configuration.MutationRepositoryClass.RepositoryClassName;

            CodeNamespace repositoryNamespace = new CodeNamespace(repositoryNamespaceValue);

            try
            {
                generatedFilesLog.Add(repositoryClassName + fileExtension, new LogElement());
                DirectoryInfo dInfo = new DirectoryInfo(outputpath);
                if (!dInfo.Exists)
                {
                    Directory.CreateDirectory(outputpath);
                }


                foreach (string ns in defaulAdditionalNamespacesForMutationRepository)
                {
                    if (!string.IsNullOrEmpty(ns))
                        repositoryNamespace.Imports.Add(new CodeNamespaceImport(ns.Trim()));
                }

                if (!string.IsNullOrEmpty(Configuration.MutationRepositoryClass.AdditionalNamespaces))
                {
                    var additionalNamespaces = Configuration.MutationRepositoryClass.AdditionalNamespaces.Split(',');
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

                var contextPrivateMemberName = addClassPrivateMember(Configuration.MutationRepositoryClass.DBContextPrivateMember
                            , ref repositoryClass);

                IList<string> parameterToFieldAssignmentStatments = new List<string>();
                IDictionary<string, string> constructorParameters = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(Configuration.MutationRepositoryClass.DBContextConstructorParameter))
                {
                    var typeAndVariableForDBContextParameter =
                        Configuration.MutationRepositoryClass.DBContextConstructorParameter.Split(' ');
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
                AddMutationRepositoryConstructor(constructorParameters, parameterToFieldAssignmentStatments);
                string returnTypePart, returnStatment;
                string tabs4 = $"\r{dicTabs["tab4"]}", tabs3 = $"\r{dicTabs["tab3"]}", tabs5 = $"\r{dicTabs["tab5"]}"
                    , tabs6 = $"\r{dicTabs["tab6"]}", tabs7 = $"\r{dicTabs["tab7"]}";
                string methodStatements = "";
                bool isDeleteField = false, isDeleteByField, isUpdateByField = false, isCreateField = false, isUpdateField = false
                    , isStoredProcedure = false;

                foreach (var m in dicMutationTypeRepositoryMapping)
                {
                    isStoredProcedure = m.Value.IsStoredProcedure;
                    foreach (var mr in m.Value.MutationFieldAndRepositoryMethodMappings)
                    {
                        methodStatements = "";

                        isDeleteField = mr.MutationFieldName.StartsWith("delete", StringComparison.OrdinalIgnoreCase);
                        isCreateField = mr.MutationFieldName.StartsWith("create", StringComparison.OrdinalIgnoreCase);
                        isUpdateField = mr.MutationFieldName.StartsWith("update", StringComparison.OrdinalIgnoreCase);
                        isUpdateByField = mr.MutationFieldName.StartsWith("update", StringComparison.OrdinalIgnoreCase)
                            && mr.MutationFieldName.Contains("By");
                        isDeleteByField = mr.MutationFieldName.StartsWith("delete", StringComparison.OrdinalIgnoreCase)
                            && mr.MutationFieldName.Contains("By");

                        var method = new CodeMemberMethod
                        {
                            Attributes = MemberAttributes.Public | MemberAttributes.Final,
                            Name = mr.RepositoryMethodName,
                        };
                        var methodInterface = new CodeMemberMethod
                        {
                            Name = mr.RepositoryMethodName,
                        };

                        var firstParam = m.Value.KeyPropertyNames?.Count > 0 ? m.Value.KeyPropertyNames[0] : null;

                        var keyParam = mr.MappingParameters.Where(p => p.IsKeyParam).FirstOrDefault();

                        var iDicParam = mr.MappingParameters
                            .Where(p => p.CSharpParameterTypeFullName == "IDictionary<string, object>").FirstOrDefault();

                        var ignoreNullValues = mr.MappingParameters
                            .Where(p => p.CSharpParameterTypeFullName == "System.Boolean").FirstOrDefault();

                        var inputTypeParam = mr.MappingParameters
                            .Where(p => (p.GraphQLParameterType.StartsWith("create", StringComparison.OrdinalIgnoreCase)
                                    || p.GraphQLParameterType.StartsWith("update", StringComparison.OrdinalIgnoreCase))
                                && p.GraphQLParameterType.EndsWith(Configuration.MutationInputTypeClasses.ClassSuffix, StringComparison.OrdinalIgnoreCase)
                            ).FirstOrDefault();
                        returnTypePart = (mr.IsFieldReturnTypeArray ? "IEnumerable<" + mr.RepositoryMethodReturnTypeFullName + ">"
                            : mr.RepositoryMethodReturnTypeFullName);

                        returnStatment = "\r" + dicTabs["tab3"] + "return Task.FromResult<" + returnTypePart
                           + ">(this." + contextPrivateMemberName + "." + m.Value.ContextProtpertyName
                           + "\r" + dicTabs["tab3"];

                        if (isStoredProcedure)
                        {
                            returnStatment = $"return Task.FromResult<{returnTypePart}" +
                                $">(this.{contextPrivateMemberName}" +
                                $".{mr.RepositoryMethodName}(";
                        }

                        if (inputTypeParam != null)
                        {
                            if (isCreateField)
                            {
                                methodStatements += "this." + contextPrivateMemberName + "." + m.Value.ContextProtpertyName
                                    + ".Add(" + inputTypeParam.RepositoryParameterName + ");";
                                methodStatements += $"\r{dicTabs["tab3"]}this.{contextPrivateMemberName}.SaveChanges();{tabs3}";
                            }

                            if (isUpdateField)
                            {
                                if (isUpdateByField)
                                {
                                    methodStatements += $"var entityToBeUpdated = this.{contextPrivateMemberName}" +
                                        $".{m.Value.ContextProtpertyName}.Find({keyParam.RepositoryParameterName});";
                                }
                                else
                                {
                                    methodStatements += $"var entityToBeUpdated = this.{contextPrivateMemberName}" +
                                        $".{m.Value.ContextProtpertyName}{tabs4}" +
                                        $".Where({iDicParam.RepositoryParameterName}){tabs4}.FirstOrDefault();";
                                }

                                methodStatements += $"\r{dicTabs["tab3"]}if (entityToBeUpdated != null)" +
                                        $"\r{dicTabs["tab3"]}";
                                methodStatements += "{" + tabs4;

                                methodStatements += $"List<PropertyInfo> propertiesToBeUpdated = " +
                                    $"{inputTypeParam.RepositoryParameterName}.GetType().GetProperties(){tabs5}" +
                                    $".Where(p => ({ignoreNullValues.RepositoryParameterName} && " +
                                    $"p.GetValue({inputTypeParam.RepositoryParameterName}) != null) " +
                                    $"|| !{ignoreNullValues.RepositoryParameterName}){tabs5}.ToList();";

                                methodStatements += $"{tabs4}foreach (var prop in propertiesToBeUpdated){tabs4}";
                                methodStatements += "{" + tabs5;

                                methodStatements += $"var propToBeUpdated = entityToBeUpdated.GetType().GetProperty(prop.Name);{tabs5}" +
                                    $"if (propToBeUpdated != null && propToBeUpdated.CanWrite){tabs5}";

                                methodStatements += "{" + tabs6;

                                methodStatements += $"propToBeUpdated.SetValue(entityToBeUpdated, prop.GetValue({inputTypeParam.RepositoryParameterName}));";

                                methodStatements += tabs5 + "}";
                                methodStatements += tabs4 + "}" + tabs4;
                                methodStatements += $"this.{contextPrivateMemberName}.SaveChanges();";
                                methodStatements += tabs3 + "}" + tabs3;
                            }

                            if (Configuration.ORMType == Configuration.ORMTypes.EFCore)
                            {
                                returnStatment += ".Where(x => x." + firstParam + " == " + inputTypeParam.RepositoryParameterName + "." + firstParam
                                + ")\r" + dicTabs["tab3"];

                                returnStatment += ".Select(selectionFields)\r" + dicTabs["tab3"] + ".FirstOrDefault())";
                            }
                            else if (Configuration.ORMType == Configuration.ORMTypes.EF6)
                            {
                                returnStatment = $"\r{dicTabs["tab3"]}var res = this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}"
                                    + $"{tabs4}.AsNoTracking(){tabs4}";
                                returnStatment += ".Where(x => x." + firstParam + " == " + inputTypeParam.RepositoryParameterName + "." + firstParam
                                    + $"){tabs4}";

                                returnStatment += ".Select(LinqDynamicExtension" +
                                    $".DynamicSelectGeneratorAnomouysType\r{dicTabs["tab5"]}<{returnTypePart}>" +
                                    $"(selectionFields)){tabs4}.ToList(){tabs4}.ToNonAnonymousList(typeof(" +
                                    $"{returnTypePart}));\r\r{dicTabs["tab3"]}var results = (IEnumerable<" +
                                    $"{returnTypePart}>)res;\r\r{dicTabs["tab3"]}";
                                returnStatment += $"return Task.FromResult<{returnTypePart}>(results.FirstOrDefault())";
                            }
                        }

                        if (isDeleteField)
                        {
                            methodStatements += $"int rowsAffected = -1;{tabs3}";
                            if (isDeleteByField)
                            {
                                methodStatements += $"var entityToBeRemoved = this.{contextPrivateMemberName}" +
                                    $".{m.Value.ContextProtpertyName}.Find({keyParam.RepositoryParameterName});";
                            }
                            else
                            {
                                methodStatements += $"var entityToBeRemoved = this.{contextPrivateMemberName}" +
                                    $".{m.Value.ContextProtpertyName}{tabs4}" +
                                    $".Where({iDicParam.RepositoryParameterName}){tabs4}.FirstOrDefault();";
                            }

                            methodStatements += $"\r{dicTabs["tab3"]}if (entityToBeRemoved != null)" +
                                    $"\r{dicTabs["tab3"]}";
                            methodStatements += "{" + tabs4;

                            methodStatements += $"this.{contextPrivateMemberName}.{m.Value.ContextProtpertyName}.Remove(entityToBeRemoved);{tabs4}";
                            methodStatements += $"rowsAffected = this.{contextPrivateMemberName}.SaveChanges();";
                            methodStatements += tabs3 + "}" + tabs3;

                            returnStatment = "\r" + dicTabs["tab3"] + "return Task.FromResult<" + returnTypePart
                          + ">(rowsAffected)";
                        }

                        int index = 0;
                        foreach (var p in mr.MappingParameters)
                        {
                            if (p.IsNullable && p.CSharpParameterTypeName != "bool")
                            {
                                method.Parameters.Add(new CodeParameterDeclarationExpression(p.CSharpParameterTypeName, p.RepositoryParameterName));
                                methodInterface.Parameters.Add(new CodeParameterDeclarationExpression(p.CSharpParameterTypeName, p.RepositoryParameterName));
                            }
                            else
                            {
                                method.Parameters.Add(new CodeParameterDeclarationExpression(p.CSharpParameterTypeFullName, p.RepositoryParameterName));
                                methodInterface.Parameters.Add(new CodeParameterDeclarationExpression(p.CSharpParameterTypeFullName, p.RepositoryParameterName));
                            }
                            index++;
                            if (isStoredProcedure)
                            {
                                if (index < mr.MappingParameters.Count)
                                    returnStatment += $"{tabs4}{p.RepositoryParameterName}: {p.RepositoryParameterName},";
                                else
                                    returnStatment += $"{tabs4}{p.RepositoryParameterName}: {p.RepositoryParameterName}))";
                            }
                        }

                        if (!isDeleteField && !isStoredProcedure)
                        {
                            method.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                            methodInterface.Parameters.Add(new CodeParameterDeclarationExpression("IEnumerable<string>", "selectionFields"));
                        }

                        method.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ">\r" + dicTabs["tab3"]);
                        methodInterface.ReturnType = new CodeTypeReference("Task<" + returnTypePart + ">\r" + dicTabs["tab3"]);

                        //if (inputTypeParam != null)
                        method.Statements.Add(new CodeSnippetExpression(methodStatements + returnStatment));
                        //else
                        //    method.Statements.Add(new CodeSnippetExpression(returnStatment));

                        int methodExcludeFilterContains = 0;
                        if (Configuration.MutationRepositoryClass.MethodExcludeFilter != null)
                        {
                            methodExcludeFilterContains = Configuration.MutationRepositoryClass.MethodExcludeFilter
                                                          .Where(x => x == mr.RepositoryMethodName).Count();
                        }
                        if (methodExcludeFilterContains < 1)
                        {
                            repositoryClass.Members.Add(method);
                            repositoryInterface.Members.Add(methodInterface);
                        }
                        else if (!Configuration.MutationRepositoryClass.IsMethodExcludeFilterApplyToInterface)
                        {
                            repositoryInterface.Members.Add(methodInterface);
                        }
                    } // End of MutationFieldAndRepositoryMethodMappings foreach
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
    }
}
