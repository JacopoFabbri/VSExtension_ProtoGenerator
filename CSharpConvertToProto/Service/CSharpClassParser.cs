using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpConvertToProto.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpConvertToProto
{
    public class CSharpClassParser
    {
        public Dictionary<string, ClassNode> ParseFolder(string folderPath)
        {
            var classNodes = new Dictionary<string, ClassNode>();
            var allClassNodes = new Dictionary<string, ClassNode>();
            var classBaseTypes = new Dictionary<string, List<string>>();
            var interfaceProperties = new Dictionary<string, List<PropertyNode>>();

            foreach (var file in Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories)
                .Where(IsFileIncludedForParsing))
            {
                var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                ParseSyntaxTree(syntaxTree, classNodes, allClassNodes, classBaseTypes, interfaceProperties);
            }

            EnrichWithInheritedProperties(classNodes, allClassNodes, classBaseTypes, interfaceProperties);
            return classNodes;
        }

        private void ParseSyntaxTree(
            SyntaxTree syntaxTree,
            Dictionary<string, ClassNode> classNodes,
            Dictionary<string, ClassNode> allClassNodes,
            Dictionary<string, List<string>> classBaseTypes,
            Dictionary<string, List<PropertyNode>> interfaceProperties)
        {
            var root = syntaxTree.GetRoot();

            // Analizza le classi
            foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classNode = new ClassNode { Name = node.Identifier.Text };

                foreach (var member in node.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (!member.Modifiers.Any(m => m.Text == "public") || member.AccessorList == null) continue;

                    classNode.Properties.Add(new PropertyNode
                    {
                        Name = member.Identifier.Text,
                        Type = member.Type.ToString()
                    });
                }

                allClassNodes[classNode.Name] = new ClassNode
                {
                    Name = classNode.Name,
                    Properties = classNode.Properties.Select(p => new PropertyNode { Name = p.Name, Type = p.Type }).ToList()
                };

                classBaseTypes[classNode.Name] = node.BaseList?.Types
                    .Select(t => ExtractSimpleTypeName(t.Type.ToString()))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList() ?? new List<string>();

                if (!IsModelClass(node)) continue;

                classNodes[classNode.Name] = classNode;
            }

            foreach (var node in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            {
                var interfaceName = node.Identifier.Text;
                interfaceProperties[interfaceName] = node.Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Select(p => new PropertyNode
                    {
                        Name = p.Identifier.Text,
                        Type = p.Type.ToString()
                    })
                    .ToList();
            }

            // Analizza gli enum
            foreach (var node in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                var enumNode = new ClassNode
                {
                    Name = node.Identifier.Text,
                    IsEnum = true
                };

                foreach (var member in node.Members)
                {
                    enumNode.EnumValues.Add(member.Identifier.Text);
                }

                classNodes[enumNode.Name] = enumNode;
            }
        }

        private void EnrichWithInheritedProperties(
            Dictionary<string, ClassNode> classNodes,
            Dictionary<string, ClassNode> allClassNodes,
            Dictionary<string, List<string>> classBaseTypes,
            Dictionary<string, List<PropertyNode>> interfaceProperties)
        {
            foreach (var classNode in classNodes.Values.Where(c => !c.IsEnum))
            {
                var existingPropertyNames = new HashSet<string>(classNode.Properties.Select(p => p.Name), System.StringComparer.OrdinalIgnoreCase);
                var inheritedProperties = CollectInheritedProperties(classNode.Name, allClassNodes, classBaseTypes, interfaceProperties, new HashSet<string>(System.StringComparer.OrdinalIgnoreCase));

                foreach (var inherited in inheritedProperties)
                {
                    if (existingPropertyNames.Add(inherited.Name))
                    {
                        classNode.Properties.Add(new PropertyNode
                        {
                            Name = inherited.Name,
                            Type = inherited.Type
                        });
                    }
                }
            }
        }

        private List<PropertyNode> CollectInheritedProperties(
            string className,
            Dictionary<string, ClassNode> allClassNodes,
            Dictionary<string, List<string>> classBaseTypes,
            Dictionary<string, List<PropertyNode>> interfaceProperties,
            HashSet<string> visited)
        {
            var properties = new List<PropertyNode>();
            if (!visited.Add(className))
            {
                return properties;
            }

            if (!classBaseTypes.TryGetValue(className, out var baseTypes) || baseTypes == null)
            {
                return properties;
            }

            foreach (var baseType in baseTypes)
            {
                if (allClassNodes.TryGetValue(baseType, out var baseClassNode) && !baseClassNode.IsEnum)
                {
                    properties.AddRange(baseClassNode.Properties.Select(p => new PropertyNode { Name = p.Name, Type = p.Type }));
                    properties.AddRange(CollectInheritedProperties(baseType, allClassNodes, classBaseTypes, interfaceProperties, visited));
                    continue;
                }

                if (interfaceProperties.TryGetValue(baseType, out var interfaceProps))
                {
                    properties.AddRange(interfaceProps.Select(p => new PropertyNode { Name = p.Name, Type = p.Type }));
                }
            }

            return properties;
        }

        private string ExtractSimpleTypeName(string rawTypeName)
        {
            if (string.IsNullOrWhiteSpace(rawTypeName))
            {
                return rawTypeName;
            }

            var typeName = rawTypeName.Trim();
            var genericIndex = typeName.IndexOf('<');
            if (genericIndex > 0)
            {
                typeName = typeName.Substring(0, genericIndex);
            }

            var lastDot = typeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < typeName.Length - 1)
            {
                typeName = typeName.Substring(lastDot + 1);
            }

            return typeName;
        }


        private bool IsModelClass(ClassDeclarationSyntax classDeclaration)
        {
            var hasBaseClass = classDeclaration.BaseList?.Types
                .Any(t => t.Type.ToString().EndsWith("Controller") ||
                          t.Type.ToString().EndsWith("Service") ||
                          t.Type.ToString().EndsWith("Repository") ||
                          t.Type.ToString().EndsWith("ViewModel")) ?? false;

            if (hasBaseClass) return false;

            var hasPublicProperties = classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Any(p => p.Modifiers.Any(m => m.Text == "public") && p.AccessorList != null);

            var hasInheritance = classDeclaration.BaseList?.Types.Any() == true;

            return hasPublicProperties || hasInheritance;
        }

        private bool IsFileIncludedForParsing(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var normalized = filePath.Replace('/', '\\');
            return normalized.IndexOf("\\bin\\", System.StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\obj\\", System.StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\.vs\\", System.StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\packages\\", System.StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
