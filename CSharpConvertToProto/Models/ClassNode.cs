using System.Collections.Generic;

namespace CSharpConvertToProto.Models
{
    public class ClassNode
    {
        public string Name { get; set; }
        public List<PropertyNode> Properties { get; set; } = new List<PropertyNode>();
        public bool IsEnum { get; set; } = false;
        public List<string> EnumValues { get; set; } = new List<string>();

        public string CustomizeNameProto(string toRemove, string customization)
        {
            if (Name.Contains(toRemove) && !string.IsNullOrWhiteSpace(customization))
            {
                return Name.Replace(toRemove, customization);
            }
            else if (string.IsNullOrWhiteSpace(customization))
            {
                return Name.Replace(toRemove, "");
            }
            else
            {
                return string.Concat(Name, customization);
            }
        }
    }
}
