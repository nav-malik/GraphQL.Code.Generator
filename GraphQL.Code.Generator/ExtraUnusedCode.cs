using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GraphQL.Code.Generator
{
    public static partial class GraphQLCodeGenerator
    {
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
                .ToDictionary(x => x.property, x => x.fkAttribute)
                .FirstOrDefault();
            }

            return FKProperty;
        }
    }
}
