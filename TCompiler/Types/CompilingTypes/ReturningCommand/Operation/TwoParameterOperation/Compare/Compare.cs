﻿using System;
using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

namespace TCompiler.Types.CompilingTypes.ReturningCommand.Operation.TwoParameterOperation.Compare
{
    public abstract class Compare : TwoParameterOperation
    {
        protected Compare(ByteVariableCall paramA, ByteVariableCall paramB) : base(paramA, paramB)
        {
        }

        protected Compare(Tuple<ByteVariableCall, ByteVariableCall> pars) : base(pars.Item1, pars.Item2)
        {
        }

        public abstract override string ToString();
    }
}