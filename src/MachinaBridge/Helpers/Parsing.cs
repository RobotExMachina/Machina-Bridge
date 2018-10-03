using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Machina;

namespace MachinaBridge
{
    public static class Parsing
    {
        /// <summary>
        /// Given a statement in the form of "Command(arg1, arg2, ...);", returns an array of clean args,
        /// with the first element being the instruction, and the rest the ordered list of args in string form
        /// without the double quotes. 
        /// </summary>
        /// <param name="statement"></param>
        /// <returns></returns>
        public static string[] ParseStatement(string statement)
        {
            try
            {
                // MEGA quick and idrty
                // assuming a msg int he form of "MoveTo(300, 400, 500);" with optional spaces here and there...  
                string[] split1 = statement.Split(new char[] { '(' });
                string[] split2 = split1[1].Split(new char[] { ')' });
                string[] args = split2[0].Split(new char[] { ',' });
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = RemoveString(RemoveSideChars(args[i], ' '), "\"");
                }
                string inst = RemoveSideChars(split1[0], ' ');

                string[] ret = new string[args.Length + 1];
                ret[0] = inst;
                for (int i = 0; i < args.Length; i++)
                {
                    ret[i + 1] = args[i];
                }

                return ret;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Given a bunch of code, splits it into clean individual statements. Removes new line chars, in-line "//" comments and splits by 
        /// </summary>
        /// <param name="program"></param>
        /// <param name="statementSeparator"></param>
        /// <returns></returns>
        public static string[] SplitStatements(string program, char statementSeparator, string inlineCommentSymbol)
        {
            // Clean new line chars
            string inline = RemoveString(program, Environment.NewLine);

            // Split by statement
            string[] statements = inline.Split(new char[] {statementSeparator}, StringSplitOptions.RemoveEmptyEntries);

            // Remove inline comments
            for (int i = 0; i < statements.Length; i++)
            {
                statements[i] = RemoveInLineComments(statements[i], inlineCommentSymbol);
            }

            // Clean preceding-trailing whitespaces
            for (int i = 0; i < statements.Length; i++)
            {
                statements[i] = RemoveSideChars(statements[i], ' ');
            }

            return statements;
        }

        /// <summary>
        /// Given a line of code, returns a new one with all inline comments removed from it.
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="commentSymbol"></param>
        /// <returns></returns>
        public static string RemoveInLineComments(string instruction, string commentSymbol)
        {
            bool commented = Regex.IsMatch(instruction, commentSymbol);
            if (!commented) return instruction;
            return instruction.Split(new string[] {commentSymbol}, StringSplitOptions.None)[0];
        }

        /// <summary>
        /// Given a string, returns a new string with all occurrences of another string within it removed.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="rem"></param>
        /// <returns></returns>
        public static string RemoveString(string str, string rem)
        {
            return str.Replace(rem, "");
        }
        
        /// <summary>
        /// Given a string, returns a new string with all preceding and trailing occurreces of another string removed.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveSideChars(string str, char rem)
        {
            if (str == "" || str == null)
                return str;

            string s = str;
            while (s[0] == rem)
            {
                s = s.Remove(0, 1);
            }

            while (s[s.Length - 1] == rem)
            {
                s = s.Remove(s.Length - 1);
            }

            return s;
        }
    }
}
