﻿namespace TCompiler.Types.CompilingTypes.Block
{
    /// <summary>
    /// The command to jump to the end of the current Block
    /// </summary>
    public class Break : Command
    {
        /// <summary>
        /// Initiates a new break command
        /// </summary>
        /// <returns>Nothing</returns>
        /// <param name="currentBlock">The current block to break</param>
        public Break(Block currentBlock) : base(false, false)
        {
            CurrentBlock = currentBlock;
        }

        /// <summary>
        /// The block the break has to break
        /// </summary>
        /// <value>The block as a Block or inheriting type of Block</value>
        public Block CurrentBlock { get; }
    }
}