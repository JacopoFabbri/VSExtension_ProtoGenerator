using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpConvertToProto
{
    public class CSharpClassParser
    {
        public Dictionary<string, ClassNode> ParseFolder(string folderPath)
        {
            var classNodes = new Dictionary<string, ClassNode>();
            foreach (var file in Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories))
            {
                var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                ParseSyntaxTree(syntaxTree, classNodes);
            }
            return classNodes;
        }

        private void ParseSyntaxTree(SyntaxTree syntaxTree, Dictionary<string, ClassNode> classNodes)
        {
            var root = syntaxTree.GetRoot();

            // Analizza le classi
            foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsModelClass(node)) continue;
                var classNode = new ClassNode { Name = node.Identifier.Text };

                foreach (var member in node.Members.OfType<PropertyDeclarationSyntax>())
                {
                    classNode.Properties.Add(new PropertyNode
                    {
                        Name = member.Identifier.Text,
                        Type = member.Type.ToString()
                    });
                }

                classNodes[classNode.Name] = classNode;
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

            return hasPublicProperties;
        }
    }

    public class ClassNode
    {
        public string Name { get; set; }
        public List<PropertyNode> Properties { get; set; } = new List<PropertyNode>();
        public bool IsEnum { get; set; } = false;
        public List<string> EnumValues { get; set; } = new List<string>();
    }

    public class PropertyNode
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
