﻿#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCompiler.Enums;
using TCompiler.Types.CompilingTypes;
using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

#endregion

namespace TCompiler.AssembleHelp
{
    /// <summary>
    /// At least theoretically a few assembler code snippets that I can use
    /// </summary>
    public static class AssembleCodePreviews
    {
        /// <summary>
        /// A code snippet that moves a single bit to the first bit of the Accu
        /// </summary>
        /// <returns>The string that has to get executed in assembler</returns>
        /// <param name="notlabel">The label to jump to if the bit is 0</param>
        /// <param name="endLabel">The label at the end (To jump over the other part)</param>
        /// <param name="bit">The bit that will be moved to the Accu</param>
        public static string MoveBitToAccu(Label notlabel, Label endLabel, BitVariableCall bit)
            => MoveBitTo(new Bool("224.0", "a0", false), notlabel, endLabel, bit.BitVariable);

        /// <summary>
        /// A code snippet that moves a single bit to a bitAddress. The destination bitAddress must be bit addressable
        /// </summary>
        /// <param name="destination">The destination bit</param>
        /// <param name="notLabel">The label to jump to if the bit is 0</param>
        /// <param name="endLabel">The label at the end (To jump over the other part)</param>
        /// <param name="bit">The bit that will be moved to the destination</param>
        /// <returns>The assembler code to execute as a string</returns>
        public static string MoveBitTo(BitVariable destination, Label notLabel, Label endLabel, BitVariable bit)
        {
            var sb = new StringBuilder();

            if (!bit.IsConstant)
            {
                sb.AppendLine($"jnb {bit.Address}, {notLabel.DestinationName}");
                sb.AppendLine($"setb {destination.Address}");
                sb.AppendLine($"jmp {endLabel.DestinationName}");
                sb.AppendLine(notLabel.LabelMark());
                sb.AppendLine($"clr {destination.Address}");
                sb.AppendLine(endLabel.LabelMark());
            }
            else
                sb.AppendLine(bit.Value ? $"setb {destination.Address}" : $"clr {destination.Address}");

            return sb.ToString();
        }

        /// <summary>
        /// The part to execute before the main program
        /// </summary>
        /// <param name="externalLabel0">The name of the external interrupt 0 Interrupt Service Routine</param>
        /// <param name="externalLabel1">The name of the external interrupt 1 Interrupt Service Routine</param>
        /// <param name="timerCounterLabel0">The name of the timer/counter interrupt 0 Interrupt Service Routine</param>
        /// <param name="timerCounterLabel1">The name of the timer/counter interrupt 1 Interrupt Service Routine</param>
        /// <param name="isCounter0">Specifies wether the timer/counter 0 is a counter</param>
        /// <param name="isCounter1">Specifies wether the timer/counter 1 is a counter</param>
        /// <returns>The assembler code to execute as a string</returns>
        public static string Before(string externalLabel0, string externalLabel1, string timerCounterLabel0, string timerCounterLabel1, bool isCounter0, bool isCounter1)
        {
            var sb = new StringBuilder();
            if (externalLabel0 == null && externalLabel1 == null && timerCounterLabel0 == null && timerCounterLabel1 == null)
                return $"{sb}main:\nmov 129, #127\n";
            sb.AppendLine("ljmp main");
            if (externalLabel0 != null)
            {
                sb.AppendLine("org 03h");
                sb.AppendLine($"call {externalLabel0}");
                sb.AppendLine("reti");
            }
            if (externalLabel1 != null)
            {
                sb.AppendLine("org 13h");
                sb.AppendLine($"call {externalLabel1}");
                sb.AppendLine("reti");
            }
            if (timerCounterLabel0 != null)
            {
                sb.AppendLine("org 0Bh");
                sb.AppendLine($"call {timerCounterLabel0}");
                sb.AppendLine("reti");
            }
            if (timerCounterLabel1 != null)
            {
                sb.AppendLine("org 1Bh");
                sb.AppendLine($"call {timerCounterLabel1}");
                sb.AppendLine("reti");
            }
            sb.AppendLine("main:");
            if (externalLabel0 != null)
            {
                sb.AppendLine("setb 088h.0");
                sb.AppendLine("clr 088h.1");
                sb.AppendLine("setb 0A8h.0");
            }
            if (externalLabel1 != null)
            {
                sb.AppendLine("setb 088h.2");
                sb.AppendLine("clr 088h.3");
                sb.AppendLine("setb 0A8h.2");
            }
            sb.AppendLine("mov 089h, #0");
            if (timerCounterLabel0 != null)
            {
                sb.AppendLine(isCounter0 ? "mov 089h, #00000101b" : "mov 089h, #00000001b");
                sb.AppendLine("setb 088h.4");
                sb.AppendLine("clr 088h.5");
                sb.AppendLine("setb 0A8h.1");
            }
            if (timerCounterLabel1 != null)
            {
                sb.AppendLine(isCounter1 ? "orl 089h, #01010000b" : "orl 089h, #00010000b");
                sb.AppendLine("setb 088h.6");
                sb.AppendLine("clr 088h.7");
                sb.AppendLine("setb 0A8h.3");
            }

            sb.AppendLine("setb 0A8h.7");
            //sb.AppendLine("mov 129, #127");
            return sb.ToString();
        }

        /// <summary>
        /// The part to execute after the normal program
        /// </summary>
        /// <returns>The assembler code to execute as a string</returns>
        public static string After()
        {
            var sb = new StringBuilder();
            sb.AppendLine("jmp main");
            sb.AppendLine("end");

            return sb.ToString();
        }

        /// <summary>
        /// The part to execute before every command, if deactivateEa is true
        /// </summary>
        /// <param name="interruptExecutions">The enabled interrupt executions</param>
        /// <returns>The assembler code to execute as a string</returns>
        public static string BeforeCommand(IEnumerable<InterruptType> interruptExecutions) => interruptExecutions.Any() ? "clr 0A8h.7" : "";

        /// <summary>
        /// The part to execute before every command, if activateEa is true
        /// </summary>
        /// <param name="interruptExecutions">The enabled interrupt executions</param>
        /// <returns>The assembler code to execute as a string</returns>
        public static string AfterCommand(IEnumerable<InterruptType> interruptExecutions ) => interruptExecutions.Any() ? "setb 0A8h.7" : "";
    }
}