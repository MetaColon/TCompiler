﻿#region

using System.Text;

using TCompiler.AssembleHelp;
using TCompiler.Types.CompilerTypes;

#endregion


namespace TCompiler.Types.CompilingTypes.ReturningCommand.Operation.TwoParameterOperation
{
    /// <summary>
    ///     Adds the two parameters<br />
    ///     Syntax:<br />
    ///     paramA + paramB
    /// </summary>
    public class Add : TwoParameterOperation
    {
        /// <summary>
        ///     Initiates a new Add operation
        /// </summary>
        /// <param name="paramA">The first parameter to add</param>
        /// <param name="paramB">The second parameter to add</param>
        /// <param name="cLine">The original T code line</param>
        public Add (ReturningCommand paramA, ReturningCommand paramB, CodeLine cLine) : base (paramA, paramB, cLine) {}

        /// <summary>
        ///     Evaluates the stuff to execute in assembler to make an add operation
        /// </summary>
        /// <returns>The assembler code as a string</returns>
        public override string ToString ()
        {
            var sb = new StringBuilder ();
            sb.AppendLine (AssembleCodePreviews.MoveParametersIntoAb (ParamA, ParamB));
            sb.AppendLine ($"{Ac.Add} A, 0F0h");
            return sb.ToString ();
        }
    }
}