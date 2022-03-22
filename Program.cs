using AmbermoonSourceror;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

const int LabelWidth = 20;
const int OpcodeWidth = 12;
const int ArgumentWidth = 36;

string CreateLine(string opcode, string arguments, string? label = null, string? comment = null)
{
    label ??= "";
    label += new string(' ', LabelWidth - label.Length);
    opcode += new string(' ', OpcodeWidth - opcode.Length);
    arguments += new string(' ', Math.Max(0, ArgumentWidth - arguments.Length));
    comment ??= "";

    if (comment.Length != 0)
        comment = new string(' ', 6) + comment.Trim();

    return label + opcode + arguments + comment;
}

var export = File.ReadAllLines(args[0]);
var source = new List<string>();
var lineRegex = new Regex(@"^([A-Z]+)_([0-9][0-9]):00([0-9a-f]{6})\s+([0-9a-f.]+)\s+([a-z.]+|\?\?)(\s+[^;]+)?(;.*)?$", RegexOptions.Compiled);
var labelRegex = new Regex(@"^\s*([a-zA-Z0-9_]+):\s*(;.*)$", RegexOptions.Compiled);
var functionRegex = new Regex(@"^;undefined .*\(\)\s*$", RegexOptions.Compiled);
var extLabelRegex = new Regex(@"[a-z]+_[0-9][0-9]:([a-z0-9_]+)", RegexOptions.Compiled);
var arrowRegex = new Regex(@"=>[^, ]+", RegexOptions.Compiled);
var indexRegex = new Regex(@"(d[0-7])[bwl]?\*\$1", RegexOptions.Compiled);
var dataRegisterSuffixRegex = new Regex(@"(d[0-7])[bw]", RegexOptions.Compiled);
var regListRegex = new Regex(@"\{\s*([ad][0-7]\s*)+\}", RegexOptions.Compiled);
var stringNameRegex = new Regex(@"s_([^,; ]+)", RegexOptions.Compiled);
var refArrowRegex = new Regex(@"\(->([^)]+)\)", RegexOptions.Compiled);
var typedDataRegex = new Regex(@"^([A-Z]+)_([0-9][0-9]):00([0-9a-f]{6})\s+([0-9a-f.]+)\s+([A-Za-z]+)(\[[0-9]+\])?(;.*)?$", RegexOptions.Compiled);
int lastHunkIndex = -1;
int hunkStartOffset = 0;
int bssOffset = 0;
string currentHunkType = "";
// Key: Label, Value = Collection of lines (indices into source)
Dictionary<string, List<int>> addressLines = new();
Dictionary<string, int> labelAddresses = new();
List<string> currentLabels = new();
int lastFunctionLine = -1;
int lineNumber = 0;
var hunkOffsets = new List<int>();
Dictionary<int, HashSet<int>> unresolvedLabels = new();
TypeParser? typeParser = null;

foreach (var line in export)
{
    ++lineNumber;
    string content = line.Trim();

    if (content.StartsWith(';'))
    {
        if (content.Contains("FUNCTION"))
            lastFunctionLine = lineNumber;

        if (lineNumber == lastFunctionLine + 2 && !content.Contains("start()") && functionRegex.IsMatch(content))
        {
            string name = line.Trim().TrimStart(';').TrimEnd(new char[] { '(', ')' })[10..].ToLower();
            currentLabels.Add(name);
            source.Add(name + ":");
        }
        else
            source.Add(line);
    }
    else if (content.StartsWith("PTR_00000004:"))
        continue;
    else if (content.StartsWith("EXEC:00000004"))
        continue;
    else
    {
        if (typeParser is not null)
        {
            if (!line.StartsWith(' '))
            {
                typeParser.Output(source);
                typeParser = null;
            }
            else
            {
                if (typeParser.ParseLine(line))
                    continue;
                else
                {
                    typeParser.Output(source);
                    typeParser = null;
                }
            }
        }

        var match = labelRegex.Match(content);

        if (match.Success)
        {
            // label
            currentLabels.Add(match.Groups[1].Value.ToLower());
            source.Add(content);
        }
        else
        {
            // CODE_00:0021f00c    6156            bsr.b       FUN_0021f064                            ;undefined FUN_0021f064()
            // BSS_10:002b7ded     00              ??          00h          

            match = lineRegex.Match(content);

            if (match.Success)
            {
                string hunkType = match.Groups[1].Value.ToUpper();
                int hunkIndex = int.Parse(match.Groups[2].Value); // 0-based
                int address = int.Parse(match.Groups[3].Value, NumberStyles.AllowHexSpecifier);
                string bytes = match.Groups[4].Value;
                string opcode = match.Groups[5].Value.ToLower();
                string arguments = (match.Groups.Count == 6 ? null : match.Groups[6].Value.TrimEnd().ToLower()) ?? "";
                string? comment = match.Groups.Count == 8 ? match.Groups[7].Value : null;
                if (comment == null && arguments.StartsWith(';'))
                {
                    comment = arguments;
                    arguments = "";
                }

                if (currentLabels.Count != 0)
                {
                    if (unresolvedLabels.TryGetValue(address, out var sourceIndices))
                    {
                        foreach (var s in sourceIndices)
                            source[s] = source[s].Replace("<label>", currentLabels[0]);
                        unresolvedLabels.Remove(address);
                    }

                    currentLabels.ForEach(currentLabel => labelAddresses.Add(currentLabel, address));
                }

                currentLabels.Clear();

                void WriteBssData(int length)
                {
                    switch (length)
                    {
                        case 1:
                            source.Add(CreateLine("dc.b", "0"));
                            break;
                        case 2:
                            if (address % 2 != 0)
                            {
                                source.Add(CreateLine("dcb.b", "2,0"));
                            }
                            else
                            {
                                source.Add(CreateLine("dc.w", "0"));
                            }
                            break;
                        case 4:
                            if (address % 2 != 0)
                            {
                                source.Add(CreateLine("dcb.b", "4,0"));
                            }
                            else
                            {
                                source.Add(CreateLine("dc.l", "0"));
                            }
                            break;
                        default:
                            if (address % 2 != 0 || length % 2 != 0)
                            {
                                source.Add(CreateLine("dcb.b", $"{length},0"));
                            }
                            else if (length % 4 != 0)
                            {
                                source.Add(CreateLine("dcb.w", $"{length / 2},0"));
                            }
                            else
                            {
                                source.Add(CreateLine("dcb.l", $"{length / 4},0"));
                            }
                            break;
                    }
                }

                if (hunkIndex != lastHunkIndex)
                {
                    if (currentHunkType == "BSS")
                    {
                        int bssSize = address - bssOffset;
                        WriteBssData(bssSize);
                    }

                    lastHunkIndex = hunkIndex;
                    hunkStartOffset = address;
                    bssOffset = hunkStartOffset;
                    currentHunkType = hunkType;
                    hunkOffsets.Add(address);

                    if (hunkIndex == 0)
                        source.Add($"start:\tSECTION\thunk{hunkIndex},{hunkType}");
                    else
                        source.Add($"hunk{hunkIndex}:\tSECTION\thunk{hunkIndex},{hunkType}");

                    if (currentHunkType == "BSS")
                        continue; // ignore opcode etc
                }
                else if (currentHunkType == "BSS")
                {
                    if (labelRegex.IsMatch(content))
                    {
                        int bssSize = address - bssOffset;
                        WriteBssData(bssSize);
                        bssOffset = address;
                        source.Add(line);
                    }

                    continue; // ignore opcode etc
                }
                else if (currentHunkType == "DATA")
                {
                    // TODO
                    continue;
                }

                string ProcessArgs()
                {
                    string result = arguments.Replace("0x", "$").Replace(",$", ",#$").Replace(",-$", ",#-$");

                    if (result.StartsWith("$") || result.StartsWith("-$"))
                        result = "#" + result;

                    result = extLabelRegex.Replace(result, match => match.Groups[1].Value);
                    result = arrowRegex.Replace(result, "");
                    result = indexRegex.Replace(result, match => match.Groups[1].Value);
                    result = dataRegisterSuffixRegex.Replace(result, match => match.Groups[1].Value);

                    if (regListRegex.IsMatch(result))
                    {
                        result = regListRegex.Replace(result, ConvertRegList);
                        result = result.Replace("sp", "(sp)");
                    }

                    var refArrowMatch = refArrowRegex.Match(result);

                    if (refArrowMatch.Success)
                    {
                        if (opcode == "movea.l" || opcode == "move.l")
                        {
                            var command = uint.Parse(bytes[0..4], NumberStyles.AllowHexSpecifier);
                            
                            if ((command & 0x38) != 0x38)
                                throw new NotSupportedException("Invalid address mode.");

                            command &= 0x7;
                            int addr = 0;

                            if (command == 2) // PC with displacement
                            {
                                // TODO: possible?
                                throw new NotSupportedException();
                            }
                            else if (command == 3) // PC with index
                            {
                                int displacement = int.Parse(bytes[4..8], NumberStyles.AllowHexSpecifier) & 0xff;
                                if (displacement >= 0x80)
                                    displacement -= 256;
                                addr = address + 2 + displacement;
                            }
                            else if (command == 1) // absolute long
                            {
                                addr = int.Parse(bytes[4..12], NumberStyles.AllowHexSpecifier);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }

                            var label = labelAddresses.FirstOrDefault(l => l.Value == addr);

                            if (label.Value != 0)
                                result = result.Replace(refArrowMatch.Value, $"({label.Key})");
                            else
                            {
                                result = result.Replace(refArrowMatch.Value, $"(<label>)");
                                if (!unresolvedLabels.TryAdd(addr, new HashSet<int> { source.Count }))
                                    unresolvedLabels[addr].Add(lineNumber);
                            }
                        }
                        else
                        {
                            throw new NotSupportedException("Arrow ref in neither movea.l nor move.l. This is not supported.");
                        }
                    }

                    return result;
                }

                if (opcode == "ds")
                {
                    opcode = "dc.b";
                    arguments += ",0";
                }
                else if (opcode == "addr")
                {
                    arguments = extLabelRegex.Replace(arguments, match => match.Groups[1].Value);
                    //addr lab_00220bc4
                    //addr DATA_04:WORD_ARRAY_XY
                    if (arguments == "00000000")
                        source.Add($"\tdc.l 0");
                    else
                    {
                        source.Add($"\tdc.l {arguments}");
                        /*if (!addressLines.TryAdd(arguments, new List<int> { source.Count }))
                            addressLines[arguments].Add(source.Count);
                        source.Add($"\tdc.l <label>");*/
                    }
                    continue;
                }
                else if ((opcode == "jmp" || opcode == "jsr") && arguments.Length == 2 && arguments[0] == 'a' && arguments[1] >= '0' && arguments[1] <= '6')
                {
                    arguments = $"({arguments})";
                }

                source.Add(CreateLine(opcode, ProcessArgs(), null, comment));
            }
            else
            {
                var typedDataMatch = typedDataRegex.Match(content);

                if (typedDataMatch.Success)
                {
                    string type = typedDataMatch.Groups[5].Value;
                    var array = typedDataMatch.Groups.Count == 6 ? "" : typedDataMatch.Groups[6].Value;
                    string comment = "";
                    
                    if (typedDataMatch.Groups.Count == 8)
                    {
                        comment = typedDataMatch.Groups[7].Value;
                    }
                    else if(array.Length != 0 && array[0] != '[')
                    {
                        comment = array;
                        array = "";
                    }

                    typeParser = new TypeParser(type, array.Length == 0 ? null : int.Parse(array.Trim(new char[] { '[', ']' })));

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
                }

                // Check for arrays, values, data, etc
                // Also do this here:
                /*if (currentLabels.Count != 0)
                {
                    if (unresolvedLabels.TryGetValue(address, out var sourceIndices))
                    {
                        foreach (var s in sourceIndices)
                            source[s] = source[s].Replace("<label>", currentLabels[0]);
                        unresolvedLabels.Remove(address);
                    }

                    currentLabels.ForEach(currentLabel => labelAddresses.Add(currentLabel, address));
                }

                currentLabels.Clear();*/

                //throw new Exception($"Invalid line [{lineNumber}]: " + Environment.NewLine + " " + line);
            }
        }
    }
}

// TODO: check remaining unsolved labels
// TODO: convert "ushort" to "dw"



/*foreach (var addressLine in addressLines)
{
    if (labelAddresses.TryGetValue(addressLine.Key, out var address))
    {
        foreach (var lineIndex in addressLine.Value)
        {
            source[lineIndex] = source[lineIndex].Replace("<label>", $"${ConvertAbsoluteAddressToRelative(address):x8}");
        }
    }
}

int ConvertAbsoluteAddressToRelative(int address)
{
    address -= 0x21f000;

    for (int i = hunkOffsets.Count - 1; i >= 0; --i)
    {
        if (address >= hunkOffsets[i])
            return address - hunkOffsets[i];
    }

    throw new Exception("Invalid address");
}*/

static string ConvertRegList(Match match)
{
    var regs = new List<string>(match.Value.Split(new char[] { '{', ' ', '}' }, StringSplitOptions.RemoveEmptyEntries));
    regs.Sort();
    string replacement = "";

    bool address = true;
    List<int> indices = new();

    foreach (var reg in regs)
    {
        bool data = reg[0] == 'd';

        if (address && data)
        {
            if (indices.Count == 1)
                replacement += $"/a{indices[0]}";
            else if (indices.Count > 1)
                replacement += $"/a{indices[0]}-a{indices[^1]}";

            address = false;
            indices.Clear();            
            indices.Add(reg[1] - '0');
        }
        else
        {
            int index = reg[1] - '0';
            if (indices.Count != 0 && indices[^1] != index - 1)
            {
                if (indices.Count == 1)
                    replacement += $"/{(address ? "a" : "d")}{indices[0]}";
                else if (indices.Count > 1)
                    replacement += $"/{(address ? "a" : "d")}{indices[0]}-{(address ? "a" : "d")}{indices[^1]}";

                indices.Clear();
            }

            indices.Add(index);
        }
    }

    if (indices.Count == 1)
        replacement += $"/{(address ? "a" : "d")}{indices[0]}";
    else if (indices.Count > 1)
        replacement += $"/{(address ? "a" : "d")}{indices[0]}-{(address ? "a" : "d")}{indices[^1]}";

    return replacement.TrimStart('/');
}

File.WriteAllLines(args[1], source, Encoding.ASCII);