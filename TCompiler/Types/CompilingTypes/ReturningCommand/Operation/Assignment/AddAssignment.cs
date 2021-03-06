﻿#region

using TCompiler.AssembleHelp;
using TCompiler.Types.CompilerTypes;
using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

#endregion


namespace TCompiler.Types.CompilingTypes.ReturningCommand.Operation.Assignment
{
    /// <summary>
    ///     An assignment that adds the evaluated value to the toAssign variable<br />
    ///     Syntax:<br />
    ///     toAssign += evaluation
    /// </summary>
    public class AddAssignment : Assignment
    {
        /// <summary>
        ///     Initiates a new AddAssignment<br />
        ///     Syntax:<br />
        ///     toAssign += evaluation
        /// </summary>
        /// <param name="toAssign">The variable to add the result</param>
        /// <param name="evaluation">The stuff to evaluate before the addassign to move the result into the Accu</param>
        /// <param name="cLine">The original T code line</param>
        public AddAssignment (Variable.Variable toAssign, ReturningCommand evaluation, CodeLine cLine) : base (toAssign,
                                                                                                               evaluation,
                                                                                                               cLine) {}

        /// <summary>
        ///     Evaluates the code to execute in assembler for an add assignment
        /// </summary>
        /// <returns>The code to execute as a string</returns>
        public override string ToString ()
            => $"{Evaluation}\n{Ac.Add} A, {ToAssign}\n{((ByteVariable) ToAssign).MoveAccuIntoThis ()}";
    }
}