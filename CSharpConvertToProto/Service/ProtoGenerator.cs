using CSharpConvertToProto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class ProtoGenerator
{
    private readonly StringBuilder _pendingMessages = new StringBuilder();
    private readonly HashSet<string> _processedClasses = new HashSet<string>();

    public string GenerateProto(IEnumerable<ClassNode> classNodes, string nameSpace, string replaceClassTextFrom = "infra", string replaceClassTextTo = "grpc")
    {
        _pendingMessages.Clear();
        _processedClasses.Clear();

        var protoBuilder = new StringBuilder();
        protoBuilder.AppendLine("syntax = \"proto3\";");
        protoBuilder.AppendLine("import \"google/protobuf/timestamp.proto\";");
        protoBuilder.AppendLine("import \"google/protobuf/wrappers.proto\";");
        protoBuilder.AppendLine("import \"google/protobuf/any.proto\";");
        protoBuilder.AppendLine($"\npackage {nameSpace};\n");

        classNodes = classNodes
            .GroupBy(n => n.Name)
            .Select(g => g.First())
            .ToList();

        var searchText = string.IsNullOrWhiteSpace(replaceClassTextFrom) ? "infra" : replaceClassTextFrom;
        var replacementText = string.IsNullOrWhiteSpace(replaceClassTextTo) ? "grpc" : replaceClassTextTo;

        foreach (var classNode in classNodes)
        {
            classNode.Name = ReplaceIgnoreCase(classNode.Name, searchText, replacementText);

            foreach (var property in classNode.Properties)
            {
                property.Type = ReplaceIgnoreCase(property.Type, searchText, replacementText);
            }
        }

        foreach (var enumNode in classNodes.Where(node => node.IsEnum))
        {
            protoBuilder.Append(GenerateEnum(enumNode));
        }

        //var rootNode = classNodes.FirstOrDefault(node => node.Name == rootClassName);
        //if (rootNode == null)
        //{
        //    throw new ArgumentException(string.Format("Classe {0} non trovata.", rootClassName));
        //}

        foreach (var classNode in classNodes.Where(n => !n.IsEnum))
        {
            protoBuilder.Append(GenerateMessage(classNode, classNodes));
        }

        //protoBuilder.Append(GenerateMessage(rootNode, classNodes));
        protoBuilder.Append(_pendingMessages);
        //protoBuilder.Append(GenerateService(rootNode, serviceTOAddAtProtoEnums));

        return protoBuilder.ToString();
    }

    private string GenerateMessage(ClassNode classNode, IEnumerable<ClassNode> classNodes)
    {
        if (_processedClasses.Contains(classNode.Name) || classNode.IsEnum)
        {
            return string.Empty;
        }

        _processedClasses.Add(classNode.Name);
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine(string.Format("message {0} {{", classNode.Name));

        int fieldIndex = 1;
        foreach (var property in classNode.Properties)
        {
            string protoType = ConvertToProtoType(property.Type, classNodes);
            messageBuilder.AppendLine(string.Format("  {0} {1} = {2};", protoType, property.Name.ToLower(), fieldIndex++));

            string propertyTypeName = ExtractBaseType(property.Type);
            var referencedClass = classNodes.FirstOrDefault(node => node.Name == propertyTypeName);
            if (referencedClass != null && !_processedClasses.Contains(propertyTypeName))
            {
                _pendingMessages.Append(GenerateMessage(referencedClass, classNodes));
            }
        }

        messageBuilder.AppendLine("}\n");
        return messageBuilder.ToString();
    }

    private string GenerateEnum(ClassNode enumNode)
    {
        var enumBuilder = new StringBuilder();
        enumBuilder.AppendLine(string.Format("enum {0} {{", enumNode.Name));
        for (int i = 0; i < enumNode.EnumValues.Count; i++)
        {
            enumBuilder.AppendLine(string.Format("  {0}_{1} = {2};", enumNode.Name, enumNode.EnumValues[i], i));
        }
        enumBuilder.AppendLine("}\n");
        return enumBuilder.ToString();
    }

    private string ConvertToProtoType(string csharpType, IEnumerable<ClassNode> classNodes)
    {
        if (string.IsNullOrWhiteSpace(csharpType))
        {
            return "string";
        }

        bool isNullable = IsNullableType(csharpType);
        string baseType = ExtractBaseType(csharpType);
        if (IsCollectionType(csharpType))
        {
            return "repeated " + ConvertToProtoType(baseType, classNodes);
        }

        //if (isNullable)
        //{
        //    switch (baseType)
        //    {
        //        case "int": return "google.protobuf.Int32Value";
        //        case "long": return "google.protobuf.Int64Value";
        //        case "float": return "google.protobuf.FloatValue";
        //        case "double": return "google.protobuf.DoubleValue";
        //        case "bool": return "google.protobuf.BoolValue";
        //        case "decimal": return "google.protobuf.DoubleValue";
        //    }
        //}

        switch (baseType)
        {
            case "int": return "int32";
            case "long": return "int64";
            case "float": return "float";
            case "double": return "double";
            case "bool": return "bool";
            case "string": return "google.protobuf.StringValue";
            case "decimal": return "double";
            case "DateTime": return "google.protobuf.StringValue";
            case "T": return "google.protobuf.Any";
            default: return classNodes.Any(node => node.Name == baseType) ? baseType : "string";
        }
    }

    private string ExtractBaseType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Il tipo fornito è nullo o vuoto.");
        }

        if (type.Contains("?"))
        {
            type = type.Replace("?", "");
        }

        int start = type.IndexOf('<');
        int end = type.LastIndexOf('>');

        return (start > 0 && end > start) ? type.Substring(start + 1, end - start - 1).Trim() : type.Trim();
    }

    private bool IsCollectionType(string type)
    {
        return !string.IsNullOrEmpty(type) && (type.StartsWith("List<") || type.StartsWith("ICollection<") || type.StartsWith("IEnumerable<") || type.StartsWith("HashSet<"));
    }

    private bool IsNullableType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        var trimmed = type.Trim();
        return trimmed.EndsWith("?", StringComparison.Ordinal) || trimmed.StartsWith("Nullable<", StringComparison.Ordinal);
    }

    private string ReplaceIgnoreCase(string input, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue))
        {
            return input;
        }

        return Regex.Replace(input, Regex.Escape(oldValue), newValue ?? string.Empty, RegexOptions.IgnoreCase);
    }
}