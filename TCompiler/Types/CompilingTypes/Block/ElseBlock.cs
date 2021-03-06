﻿using TCompiler.Types.CompilerTypes;


namespace TCompiler.Types.CompilingTypes.Block
{
    /// <summary>
    ///     The block for an else statement. <br />
    ///     This must beginn in an if block, while it appears to be the end of the if block, so that the endif is actually the
    ///     end of the else block then.<br />
    ///     Syntax:<br />
    ///     else
    /// </summary>
    public class ElseBlock : Block
    {
        /// <summary>
        ///     Initializes a new ElseBlock
        /// </summary>
        /// <param name="endLabel">The end label of the else (/if) block</param>
        /// <param name="elseLabel">The start of the else block</param>
        /// <param name="cLine">The original T code line</param>
        public ElseBlock (Label endLabel, Label elseLabel, CodeLine cLine) : base (endLabel, cLine) =>
            ElseLabel = elseLabel;

        /// <summary>
        ///     The start of the else block
        /// </summary>
        public Label ElseLabel { get; }
    }
}