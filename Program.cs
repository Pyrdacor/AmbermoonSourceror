using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

const int LabelWidth = 20;
const int OpcodeWidth = 12;
const int ArgumentWidth = 36;

string CreateLine(string opcode, string arguments, string? label = null, string? comment = null)
{
    label ??= "";
    label = label + new string(' ', LabelWidth - label.Length);
    opcode = opcode + new string(' ', OpcodeWidth - opcode.Length);
    arguments = arguments + new string(' ', Math.Max(0, ArgumentWidth - arguments.Length));
    comment ??= "";

    if (comment.Length != 0)
        comment = new string(' ', 6) + comment.Trim();

    return label + opcode + arguments + comment;
}

var export = File.ReadAllLines(args[0]);
var source = new List<string>();
var lineRegex = new Regex(@"^([A-Z]+)_([0-9][0-9]):00([0-9a-f]{6})\s+([0-9a-f.]+)\s+([a-z.]+|\?\?)\s+([^;]+)(;.*)?$", RegexOptions.Compiled);
var labelRegex = new Regex(@"^\s*[a-zA-Z0-9_]+:\s*(;.*)$", RegexOptions.Compiled);
var functionRegex = new Regex(@"^;undefined .*\(\)\s*$", RegexOptions.Compiled);
var extLabelRegex = new Regex(@"[A-Z]+_[0-9][0-9]:([A-Za-z0-9_]+)", RegexOptions.Compiled);
var arrowRegex = new Regex(@"=>[^, ]+", RegexOptions.Compiled);
var indexRegex = new Regex(@"(D[0-7][bwl]?)\*\$1", RegexOptions.Compiled);
int lastHunkIndex = -1;
int hunkStartOffset = 0;
int bssOffset = 0;
string currentHunkType = "";

foreach (var line in export)
{
    string content = line.Trim();

    if (content.StartsWith(';'))
    {
        if (!content.Contains("start()") && functionRegex.IsMatch(content))
            source.Add(line.Trim().TrimStart(';').TrimEnd(new char[] { '(', ')' })[10..] + ":");
        else
            source.Add(line);
    }
    else if (content.StartsWith("PTR_00000004:"))
        continue;
    else if (content.StartsWith("EXEC:00000004"))
        continue;
    else
    {
        // CODE_00:0021f00c    6156            bsr.b       FUN_0021f064                            ;undefined FUN_0021f064()
        // BSS_10:002b7ded     00              ??          00h          

        var match = lineRegex.Match(content);

        if (match.Success)
        {
            string hunkType = match.Groups[1].Value;
            int hunkIndex = int.Parse(match.Groups[2].Value); // 0-based
            int address = int.Parse(match.Groups[3].Value, NumberStyles.AllowHexSpecifier);
            string bytes = match.Groups[4].Value;
            string opcode = match.Groups[5].Value;
            string arguments = match.Groups[6].Value.TrimEnd();
            string? comment = match.Groups.Count == 8 ? match.Groups[7].Value : null;

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

                // TODO: replace ds "foo" by dc.b "foo",0
                // TODO: replace DXb and DXw by DX but not for index
                // TODO: jmp (AX) is added as jmp AX, so add the () back

                result = extLabelRegex.Replace(result, match => match.Groups[1].Value);
                result = arrowRegex.Replace(result, "");
                result = indexRegex.Replace(result, match => match.Groups[1].Value);

                return result;
            }

            source.Add(CreateLine(opcode, ProcessArgs(), null, comment));
        }
    }
}

File.WriteAllLines(args[1], source, Encoding.ASCII);