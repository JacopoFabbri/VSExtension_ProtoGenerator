using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CSharpConvertToProto
{
    public class CSharpClassParser
    {
        public Dictionary<string, ClassNode> ParseSolution(string solutionPath)
        {
            if (!File.Exists(solutionPath) || !solutionPath.EndsWith(".sln"))
                throw new ArgumentException("Il percorso fornito non è valido o non è una solution.");

            var workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(solutionPath).Result;

            var classNodes = new Dictionary<string, ClassNode>();

            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents.Where(d => d.FilePath.EndsWith(".cs")))
                {
                    var syntaxTree = document.GetSyntaxTreeAsync().Result;
                    if (syntaxTree != null)
                    {
                        ParseSyntaxTree(syntaxTree, classNodes);
                    }
                }
            }

            return classNodes;
        }

        private void ParseSyntaxTree(SyntaxTree syntaxTree, Dictionary<string, ClassNode> classNodes)
        {
            var root = syntaxTree.GetRoot();

            foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var classNode = new ClassNode
                {
                    Name = node.Identifier.Text
                };

                foreach (var member in node.Members.OfType<PropertyDeclarationSyntax>())
                {
                    classNode.Properties.Add(new PropertyNode
                    {
                        Name = member.Identifier.Text,
                        //Type = NormalizeType(member.Type.ToString())
                        Type = member.Type.ToString()
                    });
                }

                classNodes[classNode.Name] = classNode;
            }

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

        //private string NormalizeType(string type)
        //{
        //    return type.Replace(">", "").Trim();
        //}
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