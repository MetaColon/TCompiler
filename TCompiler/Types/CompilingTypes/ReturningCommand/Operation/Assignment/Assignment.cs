﻿using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

namespace TCompiler.Types.CompilingTypes.ReturningCommand.Operation.Assignment
{
    public class Assignment : Operation
    {
        protected ReturningCommand Evaluation { get; }
        protected Variable.Variable ToAssign { get; }

        public Assignment(Variable.Variable toAssign, ReturningCommand evaluation)
        {
            ToAssign = toAssign;
            Evaluation = evaluation;
        }

        public override string ToString()
            => ToAssign is ByteVariable
                ? (Evaluation is ByteVariableCall
                    ? $"mov {ToAssign}, {(((ByteVariableCall) Evaluation).Variable.IsConstant ? "#" + ((ByteVariableCall) Evaluation).Variable.Value : ((ByteVariableCall) Evaluation).Variable.ToString())}"
                    : $"{Evaluation}\nmov {ToAssign}, A")
                : $"{Evaluation}\nmov C, acc.0\nmov {ToAssign}, C";
    }
}