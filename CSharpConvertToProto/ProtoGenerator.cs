using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpConvertToProto;

public class ProtoGenerator
{
    private readonly StringBuilder _pendingMessages = new StringBuilder();
    private readonly HashSet<string> _processedClasses = new HashSet<string>();

    public string GenerateProto(IEnumerable<ClassNode> classNodes, string rootClassName)
    {
        _pendingMessages.Clear();
        _processedClasses.Clear();

        var protoBuilder = new StringBuilder();
        protoBuilder.AppendLine("syntax = \"proto3\";");
        protoBuilder.AppendLine("\npackage generated;\n");

        foreach (var enumNode in classNodes.Where(node => node.IsEnum))
        {
            protoBuilder.Append(GenerateEnum(enumNode));
        }

        var rootNode = classNodes.FirstOrDefault(node => node.Name == rootClassName);
        if (rootNode == null)
        {
            throw new ArgumentException(string.Format("Classe {0} non trovata.", rootClassName));
        }

        protoBuilder.Append(GenerateMessage(rootNode, classNodes));
        protoBuilder.Append(_pendingMessages);
        protoBuilder.Append(GenerateService(rootNode));

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
            enumBuilder.AppendLine(string.Format("  {0} = {1};", enumNode.EnumValues[i], i));
        }
        enumBuilder.AppendLine("}\n");
        return enumBuilder.ToString();
    }

    private string GenerateService(ClassNode rootClass)
    {
        var serviceBuilder = new StringBuilder();
        serviceBuilder.AppendLine(string.Format("service {0}Service {{", rootClass.Name));
        serviceBuilder.AppendLine(string.Format("  rpc Get{0} (Get{0}Request) returns ({0});", rootClass.Name));
        serviceBuilder.AppendLine(string.Format("  rpc Create{0} (Create{0}Request) returns ({0});", rootClass.Name));
        serviceBuilder.AppendLine("}\n");

        serviceBuilder.AppendLine(string.Format("message Get{0}Request {{", rootClass.Name));
        serviceBuilder.AppendLine("  int32 id = 1;");
        serviceBuilder.AppendLine("}\n");

        serviceBuilder.AppendLine(string.Format("message Create{0}Request {{", rootClass.Name));
        int fieldIndex = 1;
        foreach (var property in rootClass.Properties)
        {
            serviceBuilder.AppendLine(string.Format("  {0} {1} = {2};", ConvertToProtoType(property.Type, new List<ClassNode>()), property.Name.ToLower(), fieldIndex++));
        }
        serviceBuilder.AppendLine("}\n");

        return serviceBuilder.ToString();
    }

    private string ConvertToProtoType(string csharpType, IEnumerable<ClassNode> classNodes)
    {
        string baseType = ExtractBaseType(csharpType);
        if (IsCollectionType(csharpType))
        {
            return "repeated " + ConvertToProtoType(baseType, classNodes);
        }

        switch (baseType)
        {
            case "int": return "int32";
            case "long": return "int64";
            case "float": return "float";
            case "double": return "double";
            case "bool": return "bool";
            case "string": return "string";
            case "decimal": return "double";
            case "DateTime": return "string";
            default: return classNodes.Any(node => node.Name == baseType) ? baseType : "string";
        }
    }

    private string ExtractBaseType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Il tipo fornito è nullo o vuoto.");
        }

        int start = type.IndexOf('<');
        int end = type.LastIndexOf('>');

        return (start > 0 && end > start) ? type.Substring(start + 1, end - start - 1).Trim() : type.Trim();
    }

    private bool IsCollectionType(string type)
    {
        return !string.IsNullOrEmpty(type) && (type.StartsWith("List<") || type.StartsWith("ICollection<") || type.StartsWith("IEnumerable<") || type.StartsWith("HashSet<"));
    }
}