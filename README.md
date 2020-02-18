# Basic-Assembler
## Description
This basic Assembler translate every line in the code to a string that contains exactly 16 chars and represents a binary code. There is a symbol table of labels and parameters. Every line of code is being expanded by the function ExpandMacro.
List of expanded macro:
1. Increament/Decreament: "x++", "++y", "D++".
2. Direct Addressing: "x=D", "A=y".
3. Direct+Immediate Addressing: "x=5".
4. Shortcuts for loops: "D;JGT;LOOP".
This Assembler supports next Exceptions:
1. Double use of a lable that define line number.
2. Wrong syntax use.
3. Illegal definitions of lables.

## Use
This projects contains Help-Software inside dir "tools.zip" ,which implenets among the rest, assembler and simulate CPU behavior that can help you to translate and run any assemly code you like. Note: this assembler tool is'nt support Macro Expansion.

## Credits
Prof. Guy Shani for the main shield of the code, Solved and Wrriten by Daniel Ben Simon 4th year student for Software and Information Systems Engineering, Ben Gurion University of the Negev, Israel. 
