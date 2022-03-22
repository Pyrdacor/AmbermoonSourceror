using System.Globalization;
using System.Text.RegularExpressions;

namespace AmbermoonSourceror
{
    internal enum BuiltinTypeName
    {
        Byte,
        Word,
        Long,
        Addr,
        Char,
        String, // char array
        CString // null-terminated
    }

    interface IType
    {
        void Output(List<string> lines, int level);
        string? Comment { get; set; }
    }

    /*
        CODE_00:0021fa68    4c4541440021faa45...    WildcardReplace[7]                                                                                                                                              
        |_CODE_00:0021fa68    [0]                                 WildcardReplace                                                                                                                                                 
            |_CODE_00:0021fa68    WildcardText                        char[4]                         "LEAD"                                                                                                                          
                |_CODE_00:0021fa68    [0]                                 char                            'L'                                                                                                                             
                |_CODE_00:0021fa69    [1]                                 char                            'E'                                                                                                                             
                |_CODE_00:0021fa6a    [2]                                 char                            'A'                                                                                                                             
                |_CODE_00:0021fa6b    [3]                                 char                            'D'                                                                                                                             
            |_CODE_00:0021fa6c    ReplaceFunctionPtr                  addr                            FUN_ReplaceLeadWildcard                                                                                                         
        |_CODE_00:0021fa70    [1]                                 WildcardReplace                                                                                                                                                 
            |_CODE_00:0021fa70    WildcardText                        char[4]                         "SELF"                                                                                                                          
                |_CODE_00:0021fa70    [0]                                 char                            'S'                                                                                                                             
                |_CODE_00:0021fa71    [1]                                 char                            'E'                                                                                                                             
                |_CODE_00:0021fa72    [2]                                 char                            'L'                                                                                                                             
                |_CODE_00:0021fa73    [3]                                 char                            'F'                                                                                                                             
            |_CODE_00:0021fa74    ReplaceFunctionPtr                  addr                            FUN_ReplaceSelfWildcard                                                                                                         
        |_CODE_00:0021fa78    [2]                                 WildcardReplace                                                                                                                                                 
            |_CODE_00:0021fa78    WildcardText                        char[4]                         "CAST"                                                                                                                          
                |_CODE_00:0021fa78    [0]                                 char                            'C'                                                                                                                             
                |_CODE_00:0021fa79    [1]                                 char                            'A'                                                                                                                             
                |_CODE_00:0021fa7a    [2]                                 char                            'S'                                                                                                                             
                |_CODE_00:0021fa7b    [3]                                 char                            'T'                                                                                                                             
            |_CODE_00:0021fa7c    ReplaceFunctionPtr                  addr                            FUN_ReplaceCastWildcard
    */

    /*
        DATA_04:00269610    ffff000100ff00070...    AutomapWallPatternData[9]                                                                                                                                       ;-1 to +1 relative to current tile
           |_DATA_04:00269610    [0]                                 AutomapWallPatternData                                                                                                                                          
              |_DATA_04:00269610    OffsetX                             sdb                             FFh                                                                                                                             
              |_DATA_04:00269611    OffsetY                             sdb                             FFh                                                                                                                             
              |_DATA_04:00269612    NoneTileMask                        dw                              1h                                                                                                                              
           |_DATA_04:00269614    [1]                                 AutomapWallPatternData                                                                                                                                          
              |_DATA_04:00269614    OffsetX                             sdb                             0h                                                                                                                              
              |_DATA_04:00269615    OffsetY                             sdb                             FFh                                                                                                                             
              |_DATA_04:00269616    NoneTileMask                        dw                              7h
    */

    /*
        DATA_04:0026be64    ff03010104f005020...    SpellInfo[210]                                                                                                                                                  
           |_DATA_04:0026be64    [0]                                 SpellInfo                                                                                                                                                       
              |_DATA_04:0026be64    UseConditions                       SpellCondition                  WorldMap | Map2D | Map3D | Camp | Battle | Lyramion | ForestMoon | Morag                                                        
              |_DATA_04:0026be65    SP                                  db                              3h                                                                                                                              
              |_DATA_04:0026be66    SLP                                 db                              1h                                                                                                                              
              |_DATA_04:0026be67    Target                              SpellTarget                     SingleAlly                                                                                                                      
              |_DATA_04:0026be68    Element                             Element                         Unk3                                                                                                                            
           |_DATA_04:0026be69    [1]                                 SpellInfo                                                                                                                                                       
              |_DATA_04:0026be69    UseConditions                       SpellCondition                  Battle | Lyramion | ForestMoon | Morag                                                                                          
              |_DATA_04:0026be6a    SP                                  db                              5h                                                                                                                              
              |_DATA_04:0026be6b    SLP                                 db                              2h                                                                                                                              
              |_DATA_04:0026be6c    Target                              SpellTarget                     SingleAlly                                                                                                                      
              |_DATA_04:0026be6d    Element                             Element                         Unk1
    */

    /*
        BSS_01:0024bb5e     00                      Test                                                                                                                                        
           |_BSS_01:0024bb5e     Foo                                                             db          0h                                                                                                                              
        BSS_01:0024bb5f     00                      Test2                                                                                                                                       
           |_BSS_01:0024bb5f     Tes                                                             Test                                                                                                                                        
              |_BSS_01:0024bb5f     Foo                                                             db          0h                                                                                                                              
        BSS_01:0024bb60     000000000000            Test2[6]                                                                                                                                    
           |_BSS_01:0024bb60     [0]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb60     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb60     Foo                                                             db          0h                                                                                                                              
           |_BSS_01:0024bb61     [1]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb61     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb61     Foo                                                             db          0h                                                                                                                              
           |_BSS_01:0024bb62     [2]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb62     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb62     Foo                                                             db          0h                                                                                                                              
           |_BSS_01:0024bb63     [3]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb63     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb63     Foo                                                             db          0h                                                                                                                              
           |_BSS_01:0024bb64     [4]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb64     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb64     Foo                                                             db          0h                                                                                                                              
           |_BSS_01:0024bb65     [5]                                                             Test2                                                                                                                                       
              |_BSS_01:0024bb65     Tes                                                             Test                                                                                                                                        
                 |_BSS_01:0024bb65     Foo                                                             db          0h
    */

    /*
        BSS_01:0024bb6b     00000000000000000...    Test3[4]                                                                                                                                    
           |_BSS_01:0024bb6b     [0]                                                             Test3                                                                                                                                       
              |_BSS_01:0024bb6b     XYZ                                                             Test2[3]                                                                                                                                    
                 |_BSS_01:0024bb6b     [0]                                                             Test2                                                                                                                                       
                    |_BSS_01:0024bb6b     Tes                                                             Test                                                                                                                                        
                       |_BSS_01:0024bb6b     Foo                                                             db          0h                                                                                                                              
                 |_BSS_01:0024bb6c     [1]                                                             Test2                                                                                                                                       
                    |_BSS_01:0024bb6c     Tes                                                             Test                                                                                                                                        
                       |_BSS_01:0024bb6c     Foo                                                             db          0h                                                                                                                              
                 |_BSS_01:0024bb6d     [2]                                                             Test2                                                                                                                                       
                    |_BSS_01:0024bb6d     Tes                                                             Test                                                                                                                                        
                       |_BSS_01:0024bb6d     Foo                                                             db          0h                                                                                                                              
              |_BSS_01:0024bb6e     Foo                                                             ushort      0h                                                                                                                              
           |_BSS_01:0024bb70     [1]                                                             Test3                                                                                                                                       
              |_BSS_01:0024bb70     XYZ                                                             Test2[3]
    */

    internal abstract class Type : IType
    {
        public abstract void Output(List<string> lines, int level);
        public string? Comment { get; set; }
    }

    internal class BuiltinType : Type
    {
        readonly BuiltinTypeName builtinTypeName;
        readonly string value;

        public BuiltinType(BuiltinTypeName builtinTypeName, string value)
        {
            this.builtinTypeName = builtinTypeName;
            this.value = value.Trim();

            if (this.value.Contains(':'))
                this.value = this.value[(this.value.LastIndexOf(':') + 1)..];
        }

        public override void Output(List<string> lines, int level)
        {
            int StringToInt()
            {
                if (value.EndsWith('h'))
                    return int.Parse(value[0..^1], NumberStyles.AllowHexSpecifier);
                else
                    return int.Parse(value);
            }

            lines.Add(new string('\t', level + 1) + builtinTypeName switch
            {
                BuiltinTypeName.Byte => $"dc.b ${StringToInt():x2}",
                BuiltinTypeName.Word => $"dc.w ${StringToInt():x4}",
                BuiltinTypeName.Long => $"dc.l ${StringToInt():x8}",
                BuiltinTypeName.Addr => $"dc.l {value}",
                BuiltinTypeName.CString => $"dc.b {value},0",
                _ => $"dc.b {value}"
            } + " ; " + Comment);
        }
    }

    internal class StructType : Type
    {
        readonly List<IType> members = new();

        public string Name { get; }

        public StructType(string name)
        {
            Name = name;
        }

        public void AddMember(IType member)
        {
            members.Add(member);
        }

        public override void Output(List<string> lines, int level)
        {
            lines.Add(new string('\t', level + 1) + $"; struct {Name} " + Comment);

            foreach (var member in members)
                member.Output(lines, level + 1);
        }
    }

    internal class ArrayType : Type
    {
        readonly IType[] array;
        int currentIndex = 0;

        public string Name { get; }

        public ArrayType(string name, int size)
        {
            Name = name;
            array = new IType[size];
        }

        public void AddElement(IType elem)
        {
            array[currentIndex++] = elem;
        }

        public override void Output(List<string> lines, int level)
        {
            if (currentIndex != array.Length)
                throw new NullReferenceException($"Missing array elements for array {Name}[{array.Length}].");

            lines.Add(new string('\t', level + 1) + $"; {Name}[{array.Length}] " + Comment);

            foreach (var elem in array)
                elem.Output(lines, level + 1);
        }
    }

    internal class TypeParser
    {
        record Node(IType Type, Node? Parent, List<Node> Children, int Identation);

        const int SpacesPerTab = 3;
        static readonly Regex TypeRegex = new(@"([ ]+)\|_(CODE|DATA|BSS)_([0-9]+):00[0-9a-f]{6}\s+([A-Za-z0-9]+)\s+([a-zA-Z0-9\[\]]+)(\s+[^; ]+)?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex TypeArrayElementRegex = new(@"([ ]+)\|_(CODE|DATA|BSS)_([0-9]+):00[0-9a-f]{6}\s+\[([0-9]+)\]\s+([a-zA-Z0-9]+)(\s+[^; ]+)?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        int linesToSkip = 0;
        ArrayType? arrayType = null;
        readonly Node rootNode;
        Node currentNode;

        public TypeParser(string rootTypeName, int? arraySize)
        {
            rootNode = currentNode = new Node(arraySize is null
                ? new StructType(rootTypeName)
                : new ArrayType(rootTypeName, arraySize.Value),
                null, new(), 0);

            if (arraySize is not null)
                arrayType = rootNode.Type as ArrayType;
        }

        public void Output(List<string> lines)
        {
            rootNode.Type.Output(lines, 0);
        }

        public bool ParseLine(string line)
        {
            if (line.Length == 0)
                return false;

            var match = TypeRegex.Match(line);

            if (match.Success)
            {
                int identation = match.Groups[1].Length / SpacesPerTab;
                string name = match.Groups[4].Value;
                string typename = match.Groups[5].Value;
                string? value = match.Groups.Count == 6 ? null : match.Groups[6].Value.Trim();

                var result = ParseType(typename, value);

                if (result is not null)
                {
                    result.Comment = name;
                    AddNode(identation, result);

                    if (currentNode?.Type is ArrayType nodeArrayType)
                        arrayType = nodeArrayType;
                    else
                        arrayType = null;
                }

                return true;
            }

            match = TypeArrayElementRegex.Match(line);

            if (match.Success)
            {
                if (arrayType is null)
                    throw new Exception("Array element line without array type.");

                int identation = match.Groups[1].Length / SpacesPerTab;
                int index = int.Parse(match.Groups[4].Value);
                string typename = match.Groups[5].Value;
                string? value = match.Groups.Count == 6 ? null : match.Groups[6].Value.Trim();

                var result = ParseType(typename, value);

                if (result is not null)
                {
                    result.Comment = $"[{index}]";
                    AddNode(identation, result);
                }

                return true;
            }

            return false;

            void AddNode(int identation, IType type)
            {
                while (currentNode!.Identation + 1 != identation && currentNode.Parent is not null)
                    currentNode = currentNode.Parent;
                var child = new Node(type, currentNode, new(), identation);
                currentNode.Children.Add(child);

                if (currentNode.Type is StructType structType)
                    structType.AddMember(type);
                else if (currentNode.Type is ArrayType arrayType)
                    arrayType.AddElement(type);

                if (type is StructType || type is ArrayType)
                    currentNode = child;
            }
        }

        IType? ParseType(string typename, string? value)
        {
            if (linesToSkip > 0)
            {
                --linesToSkip;
                return null;
            }

            switch (typename.ToLower())
            {
                case "addr":
                    return new BuiltinType(BuiltinTypeName.Addr, value ?? throw new ArgumentNullException(nameof(value)));
                case "long":
                case "dl":
                case "ddw":
                    return new BuiltinType(BuiltinTypeName.Long, value ?? throw new ArgumentNullException(nameof(value)));
                case "short":
                case "ushort":
                case "dw":
                    return new BuiltinType(BuiltinTypeName.Word, value ?? throw new ArgumentNullException(nameof(value)));
                case "byte":
                case "sbyte":
                case "db":
                case "sdb":
                    return new BuiltinType(BuiltinTypeName.Byte, value ?? throw new ArgumentNullException(nameof(value)));
                case "ds":
                    return new BuiltinType(BuiltinTypeName.CString, value ?? throw new ArgumentNullException(nameof(value)));
                case "char":
                    return new BuiltinType(BuiltinTypeName.Char, value ?? throw new ArgumentNullException(nameof(value)));
                case string x when x.StartsWith("char["):
                    linesToSkip = int.Parse(x[5..^1]);
                    return new BuiltinType(BuiltinTypeName.String, value ?? throw new ArgumentNullException(nameof(value)));
                case string x when x.EndsWith(']'):
                    {
                        int index = x.IndexOf('[');
                        int size = int.Parse(x[(index + 1)..^1]);
                        return new ArrayType(x[..index], size);
                    }
                default:
                    return new StructType(typename.ToLower());
            }
        }
    }
}
