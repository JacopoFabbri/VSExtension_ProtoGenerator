using System;

namespace CSharpConvertToProto.Models
{
    [Serializable]
    public class Settings
    {
        public string SourceFolder { get; set; }
        public string OutputFolder { get; set; }
        public string NameSpace { get; set; }
        public string TargetFolderName { get; set; } = "ModelsForGeneration";
    }
}
