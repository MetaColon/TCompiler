﻿#region

using System.Collections.Generic;
using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

#endregion

namespace TCompiler.Types.CompilingTypes.Block
{
    public class Block : Command
    {
        public readonly List<Variable> Variables;

        public Block(Label endLabel)
        {
            Variables = new List<Variable>();
            EndLabel = endLabel;
        }

        public Label EndLabel { get; set; }
    }
}