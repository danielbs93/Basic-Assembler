using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assembler
{
    public class Assembler
    {
        private const int WORD_SIZE = 16;

        private Dictionary<string, int[]> m_dControl, m_dJmp, m_dDest; //these dictionaries map command mnemonics to machine code - they are initialized at the bottom of the class
        private Dictionary<string, int> Symbol_Table;
        //more data structures here (symbol map, ...)

        public Assembler()
        {
            InitCommandDictionaries();
            initSavedReg_List();
        }

        //this method is called from the outside to run the assembler translation
        public void TranslateAssemblyFile(string sInputAssemblyFile, string sOutputMachineCodeFile)
        {
            initSavedReg_List();
            //read the raw input, including comments, errors, ...
            StreamReader sr = new StreamReader(sInputAssemblyFile);
            List<string> lLines = new List<string>();
            while (!sr.EndOfStream)
            {
                lLines.Add(sr.ReadLine());
            }
            sr.Close();
            //translate to machine code
            List<string> lTranslated = TranslateAssemblyFile(lLines);
            //write the output to the machine code file
            StreamWriter sw = new StreamWriter(sOutputMachineCodeFile);
            foreach (string sLine in lTranslated)
                sw.WriteLine(sLine);
            sw.Close();
        }

        //translate assembly into machine code
        private List<string> TranslateAssemblyFile(List<string> lLines)
        {
            InitCommandDictionaries();
            //init data structures here 

            //expand the macros
            List<string> lAfterMacroExpansion = ExpendMacros(lLines);

            //first pass - create symbol table and remove lable lines
            CreateSymbolTable(lAfterMacroExpansion);

            //second pass - replace symbols with numbers, and translate to machine code
            List<string> lAfterTranslation = TranslateAssemblyToMachineCode(lAfterMacroExpansion);
            return lAfterTranslation;
        }

        
        //first pass - replace all macros with real assembly
        private List<string> ExpendMacros(List<string> lLines)
        {
            List<string> lAfterExpansion = new List<string>();
            for (int i = 0; i < lLines.Count; i++)
            {
                //remove all redudant characters
                string sLine = CleanWhiteSpacesAndComments(lLines[i]);
                if (sLine == "")
                    continue;
                //if the line contains a macro, expand it, otherwise the line remains the same
                List<string> lExpanded = ExapndMacro(sLine);
                //we may get multiple lines from a macro expansion
                foreach (string sExpanded in lExpanded)
                {
                    lAfterExpansion.Add(sExpanded);
                }
            }
            return lAfterExpansion;
        }

        //expand a single macro line
        private List<string> ExapndMacro(string sLine)
        {
            Invalid_Input(sLine);
            List<string> lExpanded = new List<string>();

            if (IsCCommand(sLine))
            {
                string sDest, sCompute, sJmp;
                GetCommandParts(sLine, out sDest, out sCompute, out sJmp);
                if (!(sDest.Equals("M") || sDest.Equals("D") || sDest.Equals("A")) && sJmp == "")
                {
                    if ((sCompute.Contains("++") || sCompute.Contains("--")) && sDest == "")
                    {
                        if (sCompute.Contains("--"))
                        {
                            if (sCompute.Equals("M") || sCompute.Equals("D") || sCompute.Equals("A"))
                                lExpanded.Add(sCompute + "=" + sCompute + "-1");
                            else
                            {
                                lExpanded.Add("@" + sCompute.Substring(0,sCompute.Length - 2));
                                lExpanded.Add("M=M-1");
                            }
                        }
                        else if (sCompute.Contains("++"))
                        {
                            if (sCompute.Equals("M") || sCompute.Equals("D") || sCompute.Equals("A"))
                                 lExpanded.Add(sCompute + "=" + sCompute + "+1");
                            else
                            {
                                 lExpanded.Add("@" + sCompute.Substring(0,sCompute.Length - 2));
                                 lExpanded.Add("M=M+1");    
                            }
                        }
                    }
                    else if (sJmp == "" && sLine.Contains("="))
                    {
                        if (sDest.Equals("D") || sDest.Equals("A"))
                        {
                            if (!(sCompute.Contains("M") || sCompute.Contains("D") || sCompute.Contains("A") || char.IsDigit(sCompute[0])))
                            {
                                lExpanded.Add("@" + sCompute);
                                lExpanded.Add(sDest + "=M");
                            }
                            else if (char.IsDigit(sCompute[0]))
                            {
                                lExpanded.Add("@" + sCompute);
                                lExpanded.Add(sDest + "=A");
                            }

                        }
                        else
                        {
                            if (sCompute.Contains("M") || sCompute.Contains("A"))
                            {
                                if (sCompute.Contains("+") && !(sCompute[sCompute.Length-1] == 'M' || sCompute[sCompute.Length-1] == 'A'))
                                {
                                    lExpanded.Add("D=" + sCompute[0]);
                                    lExpanded.Add("@" + sCompute[sCompute.Length-1]);
                                    lExpanded.Add("D=D+A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add(sCompute[0] + "=D");
                                }
                                else if (sCompute.Contains("+") && (sCompute[sCompute.Length-1] == 'M' || sCompute[sCompute.Length-1] == 'A'))
                                {
                                    lExpanded.Add("D=" + sCompute[sCompute.Length-1]);
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=D+A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add(sCompute[sCompute.Length - 1] + "=D");
                                }
                                else if (sCompute.Contains("-") && !(sCompute[sCompute.Length-1] == 'M' || sCompute[sCompute.Length-1] == 'A'))
                                {
                                    lExpanded.Add("D=" + sCompute[0]);
                                    lExpanded.Add("@" + sCompute[sCompute.Length-1]);
                                    lExpanded.Add("D=D-A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add(sCompute[0] + "=D");
                                }
                                else if (sCompute.Contains("-") && (sCompute[sCompute.Length-1] == 'M' || sCompute[sCompute.Length-1] == 'A'))
                                {
                                    lExpanded.Add("D=" + sCompute[sCompute.Length-1]);
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=D-A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add(sCompute[sCompute.Length - 1] + "=D");
                                }else
                                {
                                    lExpanded.Add("D=" + sCompute);
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                            }
                            else if (sCompute.Contains("D"))
                            {
                                if (sCompute.Contains("+") && !(sCompute[sCompute.Length - 1] == 'D'))
                                {
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("D=D+A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else if (sCompute.Contains("+") && sCompute[sCompute.Length - 1] == 'D')
                                {
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=D+A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else if (sCompute.Contains("-") && !(sCompute[sCompute.Length - 1] == 'D'))
                                {
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("D=D-A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else if (sCompute.Contains("-") && sCompute[sCompute.Length - 1] == 'D')
                                {
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=D-A");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else
                                {
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                            } 
                            else if (sCompute.Contains("+"))
                            {
                                if (char.IsDigit(sCompute[0]))
                                {
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=A");
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("M=M+D");
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else
                                {
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("D=A");
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("M=M+D");
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                            }
                            else if (sCompute.Contains("-"))
                            {
                                if (char.IsDigit(sCompute[0]))
                                {
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("D=A");
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("M=M-D");
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                                else
                                {
                                    lExpanded.Add("@" + sCompute[sCompute.Length - 1]);
                                    lExpanded.Add("D=A");
                                    lExpanded.Add("@" + sCompute[0]);
                                    lExpanded.Add("M=M-D");
                                    lExpanded.Add("D=M");
                                    lExpanded.Add("@" + sDest);
                                    lExpanded.Add("M=D");
                                }
                            }
                            else if (!char.IsDigit(sCompute[0]))
                            {
                                lExpanded.Add("@" + sCompute);
                                lExpanded.Add("D=M");
                                lExpanded.Add("@" + sDest);
                                lExpanded.Add("M=D");
                            }
                            else
                            {
                                lExpanded.Add("@" + sCompute);
                                lExpanded.Add("D=A");
                                lExpanded.Add("@" + sDest);
                                lExpanded.Add("M=D");
                            }
                        }
                    }
                }
                else if (sJmp == "" && sLine.Contains("=") && !(sDest.Equals("M")) && !sCompute.Equals("1") && !sCompute.Equals("0"))
                {
                    if (sDest.Equals("D") || sDest.Equals("A"))
                    {
                        if (!(sCompute.Contains("M") || sCompute.Contains("D") || sCompute.Contains("A") || char.IsDigit(sCompute[0])))
                        {
                            lExpanded.Add("@" + sCompute);
                            lExpanded.Add(sDest + "=M");
                        }
                        else if (char.IsDigit(sCompute[0]))
                        {
                            lExpanded.Add("@" + sCompute);
                            lExpanded.Add(sDest + "=A");
                        }

                    }
                    else
                    {
                        if (sCompute.Equals("M") || sCompute.Equals("A"))
                        {
                            lExpanded.Add("D=" + sCompute);
                            lExpanded.Add("@" + sDest);
                            lExpanded.Add("M=D");
                        }
                        else if (sCompute.Equals("D"))
                        {
                            lExpanded.Add("@" + sDest);
                            lExpanded.Add("M=D");
                        } 
                        else if (!char.IsDigit(sCompute[0]))
                        {
                            lExpanded.Add("@" + sCompute);
                            lExpanded.Add("D=M");
                            lExpanded.Add("@" + sDest);
                            lExpanded.Add("M=D");
                        }
                        else
                        {
                            lExpanded.Add("@" + sCompute);
                            lExpanded.Add("D=A");
                            lExpanded.Add("@" + sDest);
                            lExpanded.Add("M=D");
                        }
                    }
                }
                
                else if (sJmp.Contains(":"))
                {
                    string jmpLable = sJmp.Substring(sJmp.IndexOf(":") + 1, sJmp.Length - (sJmp.IndexOf(":") + 1));
                    lExpanded.Add("@" + jmpLable);
                    lExpanded.Add(sCompute + ";" + sJmp.Substring(0,3));
                }
            }
            if (lExpanded.Count == 0)
                lExpanded.Add(sLine);
            return lExpanded;
        }

        //second pass - record all symbols - labels and variables
        private void CreateSymbolTable(List<string> lLines)
        {
            string sLine = "";
            for (int i = 0; i < lLines.Count; i++)
            {
                sLine = lLines[i];
                Invalid_Input(sLine);
                if (IsLabelLine(sLine))
                {
                    int lableLineCode = -1;
                    for (int j = 0; j <= i; j++)
                    {
                        if (!IsLabelLine(lLines[j]) && CleanWhiteSpacesAndComments(lLines[j]) != "")
                            lableLineCode++;
                    }
                    if (!Symbol_Table.ContainsKey(sLine.Substring(1,sLine.Length-2)))
                        Symbol_Table.Add(sLine.Substring(1,sLine.Length-2),lableLineCode+1);
                    else
                    {
                        throw new Exception ("Double Lable!");
                    }
                }
                else if (IsACommand(sLine))
                {
                    string lable = "(" + sLine.Substring(1,sLine.Length-1) + ")";
                    Boolean islableExist = false;
                    for (int j = 0; j < lLines.Count; j++)
                        if (lLines[j].Equals(lable))
                            islableExist = true;
                    if(islableExist == false && !char.IsDigit(sLine[1]))
                        if (!Symbol_Table.ContainsKey(sLine.Substring(1,sLine.Length - 1)))
                            Symbol_Table.Add(sLine.Substring(1,sLine.Length - 1), -1);
                }
                else if (IsCCommand(sLine))
                {
                    //do nothing here
                }
                else
                    throw new FormatException("Cannot parse line " + i + ": " + lLines[i]);
            }
          
        }
        
        //third pass - translate lines into machine code, replaicng symbols with numbers
        private List<string> TranslateAssemblyToMachineCode(List<string> lLines)
        {
            int nextFreeReg = 16;
            string sLine = "";
            List<string> lAfterPass = new List<string>();
            for (int i = 0; i < lLines.Count; i++)
            {
                int Acmd = 0;
                sLine = lLines[i];
                if (IsACommand(sLine))
                {
                    if (!char.IsDigit(sLine[1]) && Symbol_Table[sLine.Substring(1,sLine.Length - 1)] == -1)
                    {
                        Symbol_Table[sLine.Substring(1,sLine.Length - 1)] = nextFreeReg;
                        Acmd = nextFreeReg;
                        nextFreeReg++;
                    }
                    else if (!char.IsDigit(sLine[1]))
                            Acmd = Symbol_Table[sLine.Substring(1,sLine.Length - 1)];
                         else 
                           for (int j = sLine.Length - 1, k = 0; j > 0; j--, k++)
                                Acmd += (int) ((char.GetNumericValue(sLine[j]))*(Math.Pow(10,k)));   
                   
                    lAfterPass.Add(ToBinary(Acmd));
                }
                else if (IsCCommand(sLine))
                {
                    string Ccmd = "111";
                    string sDest, sControl, sJmp;
                    GetCommandParts(sLine, out sDest, out sControl, out sJmp);
                    if (Symbol_Table.ContainsKey(sDest))
                        sDest = Symbol_Table[sDest].ToString();
                    if (Symbol_Table.ContainsKey(sControl))
                        sControl = Symbol_Table[sControl].ToString();
                    Ccmd += ToString(m_dControl[sControl]);
                    Ccmd += ToString(m_dDest[sDest]);
                    Ccmd += ToString(m_dJmp[sJmp]);
                    lAfterPass.Add(Ccmd);
                    
                }
                else if (IsLabelLine(sLine))
                        continue;
                else
                    throw new FormatException("Cannot parse line " + i + ": " + lLines[i]);
            }
            return lAfterPass;
        }

        //helper functions for translating numbers or bits into strings

        private Exception Invalid_Input(string sLine)
        {
            string sDest,sCompute,sJmp;
            GetCommandParts(sLine, out sDest, out sCompute, out sJmp);
            if (sLine[0] == '@' && sLine[1] == '-')
                throw new Exception("Positive Numbers Only!");
            if (sLine[0] == '@' && char.IsDigit(sLine[1]))
            {
                for (int i = 2; i < sLine.Length; i++)
                    {
                        if(char.IsLetter(sLine[i]))
                        throw new Exception("Invalid Syntax");
                   }
            }

        return null;
        }

        private string ToString(int[] aBits)
        {
            string sBinary = "";
            for (int i = 0; i < aBits.Length; i++)
                sBinary += aBits[i];
            return sBinary;
        }

        private string ToBinary(int x)
        {
            string sBinary = "";
            for (int i = 0; i < WORD_SIZE; i++)
            {
                sBinary = (x % 2) + sBinary;
                x = x / 2;
            }
            return sBinary;
        }


        //helper function for splitting the various fields of a C command
        private void GetCommandParts(string sLine, out string sDest, out string sControl, out string sJmp)
        {
            if (sLine.Contains('='))
            {
                int idx = sLine.IndexOf('=');
                sDest = sLine.Substring(0, idx);
                sLine = sLine.Substring(idx + 1);
            }
            else
                sDest = "";
            if (sLine.Contains(';'))
            {
                int idx = sLine.IndexOf(';');
                sControl = sLine.Substring(0, idx);
                sJmp = sLine.Substring(idx + 1);

            }
            else
            {
                sControl = sLine;
                sJmp = "";
            }
        }

        private bool IsCCommand(string sLine)
        {
            return !IsLabelLine(sLine) && sLine[0] != '@';
        }

        private bool IsACommand(string sLine)
        {
            return sLine[0] == '@';
        }

        private bool IsLabelLine(string sLine)//label of jumping
        {
            if (sLine.StartsWith("(") && sLine.EndsWith(")"))
                return true;
            return false;
        }

        private string CleanWhiteSpacesAndComments(string sDirty)
        {
            string sClean = "";
            for (int i = 0 ; i < sDirty.Length ; i++)
            {
                char c = sDirty[i];
                if (c == '/' && i < sDirty.Length - 1 && sDirty[i + 1] == '/') // this is a comment
                    return sClean;
                if (c > ' ' && c <= '~')//ignore white spaces
                    sClean += c;
            }
            return sClean;
        }


        private void InitCommandDictionaries()
        {
            m_dDest = new Dictionary<string, int[]>();
            m_dDest[""] = new int[]{0,0,0};
            m_dDest["M"] = new int[]{0,0,1};
            m_dDest["D"] = new int[]{0,1,0};
            m_dDest["MD"] = new int[]{0,1,1};
            m_dDest["A"] = new int[]{1,0,0};
            m_dDest["AM"] = new int[]{1,0,1};
            m_dDest["AD"] = new int[]{1,1,0};
            m_dDest["AMD"] = new int[]{1,1,1};

            m_dControl = new Dictionary<string, int[]>();

            m_dControl["0"] = new int[] { 0, 1, 0, 1, 0, 1, 0 };
            m_dControl["1"] = new int[] { 0, 1, 1, 1, 1, 1, 1 };
            m_dControl["-1"] = new int[] { 0, 1, 1, 1, 0, 1, 0 };
            m_dControl["D"] = new int[] { 0, 0, 0, 1, 1, 0, 0 };
            m_dControl["A"] = new int[] { 0, 1, 1, 0, 0, 0, 0 };
            m_dControl["!D"] = new int[] { 0, 0, 0, 1, 1, 0, 1 };
            m_dControl["!A"] = new int[] { 0, 1, 1, 0, 0, 0, 1 };
            m_dControl["-D"] = new int[] { 0, 0, 0, 1, 1, 1, 1 };
            m_dControl["-A"] = new int[] { 0, 1, 1, 0, 0,1, 1 };
            m_dControl["D+1"] = new int[] { 0, 0, 1, 1, 1, 1, 1 };
            m_dControl["A+1"] = new int[] { 0, 1, 1, 0, 1, 1, 1 };
            m_dControl["D-1"] = new int[] { 0, 0, 0, 1, 1, 1, 0 };
            m_dControl["A-1"] = new int[] { 0, 1, 1, 0, 0, 1, 0 };
            m_dControl["D+A"] = new int[] { 0, 0, 0, 0, 0, 1, 0 };
            m_dControl["A+D"] = new int[] { 0, 0, 0, 0, 0, 1, 0 };
            m_dControl["D-A"] = new int[] { 0, 0, 1, 0, 0, 1, 1 };
            m_dControl["A-D"] = new int[] { 0, 0, 0, 0, 1,1, 1 };
            m_dControl["D&A"] = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            m_dControl["D|A"] = new int[] { 0, 0, 1, 0,1, 0, 1 };

            m_dControl["M"] = new int[] { 1, 1, 1, 0, 0, 0, 0 };
            m_dControl["!M"] = new int[] { 1, 1, 1, 0, 0, 0, 1 };
            m_dControl["-M"] = new int[] { 1, 1, 1, 0, 0, 1, 1 };
            m_dControl["M+1"] = new int[] { 1, 1, 1, 0, 1, 1, 1 };
            m_dControl["M-1"] = new int[] { 1, 1, 1, 0, 0, 1, 0 };
            m_dControl["D+M"] = new int[] { 1, 0, 0, 0, 0, 1, 0 };
            m_dControl["M+D"] = new int[] { 1, 0, 0, 0, 0, 1, 0 };
            m_dControl["D-M"] = new int[] { 1, 0, 1, 0, 0, 1, 1 };
            m_dControl["M-D"] = new int[] { 1, 0, 0, 0, 1, 1, 1 };
            m_dControl["D&M"] = new int[] { 1, 0, 0, 0, 0, 0, 0 };
            m_dControl["D|M"] = new int[] { 1, 0, 1, 0, 1, 0, 1 };


            m_dJmp = new Dictionary<string, int[]>();

            m_dJmp[""] = new int[] { 0, 0, 0 };
            m_dJmp["JGT"] = new int[] { 0, 0, 1 };
            m_dJmp["JEQ"] = new int[] { 0, 1, 0 };
            m_dJmp["JGE"] = new int[] { 0, 1, 1 };
            m_dJmp["JLT"] = new int[] { 1, 0, 0 };
            m_dJmp["JNE"] = new int[] { 1, 0, 1 };
            m_dJmp["JLE"] = new int[] { 1, 1, 0 };
            m_dJmp["JMP"] = new int[] { 1, 1, 1 };
        }

        private void initSavedReg_List()
        {
            Symbol_Table = new Dictionary<string, int>();
            Symbol_Table["R0"] = 0;
            Symbol_Table["R1"] = 1;
            Symbol_Table["R2"] = 2;
            Symbol_Table["R3"] = 3;
            Symbol_Table["R4"] = 4;
            Symbol_Table["R5"] = 5;
            Symbol_Table["R6"] = 6;
            Symbol_Table["R7"] = 7;
            Symbol_Table["R8"] = 8;
            Symbol_Table["R9"] = 9;
            Symbol_Table["R10"] = 10;
            Symbol_Table["R11"] = 11;
            Symbol_Table["R12"] = 12;
            Symbol_Table["R13"] = 13;
            Symbol_Table["R14"] = 14;
            Symbol_Table["R15"] = 15;
            Symbol_Table["SCREEN "] = 16384;
            Symbol_Table["KBD"] = 24576;

        }
    }
}
