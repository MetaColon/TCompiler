﻿using System;
using System.Collections.Generic;
using System.Text;
using TCompiler.Enums;
using TCompiler.Types;
using TCompiler.Types.CompilingTypes;
using TCompiler.Types.CompilingTypes.Block;
using TCompiler.Types.CompilingTypes.ReturningCommand.Method;
using TCompiler.Types.CompilingTypes.ReturningCommand.Variable;

namespace TCompiler.Compiling
{
    public static class ParseToAssembler
    {
        private static int _byteCounter = 0x30;
        private static readonly IntPair _bitCounter = new IntPair(0x20, 0x2F);
        private static int _label = 0;

        private static int ByteCounter
        {
            get
            {
                _byteCounter++;
                return _byteCounter;
            }
            set { _byteCounter = value; }
        }

        private static IntPair BitCounter
        {
            get
            {
                IncreaseBitCounter();
                return _bitCounter;
            }
        }

        private static void IncreaseBitCounter()
        {
            if (BitCounter.Item2 < 7)
                BitCounter.Item2++;
            else
            {
                BitCounter.Item1++;
                BitCounter.Item2 = 0;
            }
        }

        private static void DecreaseBitCounter()
        {
            if (BitCounter.Item2 > 0)
                BitCounter.Item2--;
            else
            {
                BitCounter.Item1--;
                BitCounter.Item2 = 7;
            }
        }

        public static string Label1
        {
            get
            {
                _label++;
                return $"l{_label}";
            }
        }

        public static string ParseObjectsToAssembler(IEnumerable<Command> commands)
        {
            _label = 0;
            var fin = new StringBuilder();
            fin.AppendLine("include reg8051.inc");

            foreach (var command in commands)
            {
                var t = command.GetType();
                CommandType ct;
                if (Enum.TryParse(t.Name, true, out ct))
                {
                    switch (ct)
                    {
                        case CommandType.Block:
                            break;
                        case CommandType.EndBlock:
                        {
                            var eb = (EndBlock) command;
                            var bt = eb.Block.GetType();

                            if (bt == typeof(WhileBlock))
                                fin.AppendLine($"jmp {((WhileBlock) eb.Block).UpperLabel}");
                            else if (bt == typeof(ForTilBlock))
                                fin.AppendLine($"djnz A, {((ForTilBlock) eb.Block).UpperLabel}");

                            fin.AppendLine(eb.Block.EndLabel.Name);
                            foreach (var variable in eb.Block.Variables)
                            {
                                if (variable is ByteVariable)
                                    ByteCounter--;
                                else
                                    DecreaseBitCounter();
                            }
                            break;
                        }
                        case CommandType.IfBlock:
                        {
                            var ib = (IfBlock) command;
                            fin.AppendLine(ib.Condition.ToString());
                            fin.AppendLine($"jnb acc.0, {ib.EndLabel}");
                            break;
                        }
                        case CommandType.WhileBlock:
                        {
                            var wb = (WhileBlock) command;
                            fin.AppendLine($"{wb.UpperLabel}:");
                            fin.AppendLine(wb.Condition.ToString());
                            fin.AppendLine($"jnb acc.0, {wb.EndLabel}");
                            break;
                        }
                        case CommandType.ForTilBlock:
                        {
                            var ftb = (ForTilBlock) command;
                            fin.AppendLine($"mov A, {ftb.Limit}");
                            fin.AppendLine($"{Label1}:");
                            break;
                        }
                        case CommandType.Break:
                        {
                            var b = (Break) command;
                            fin.AppendLine($"jmp {b.CurrentBlockEndLabel}");
                            break;
                        }
                        case CommandType.Method:
                        {
                            var m = (Method) command;
                            fin.AppendLine($"{m.Name}:");
                            break;
                        }
                        case CommandType.EndMethod:
                            fin.AppendLine("ret");

                            foreach (var variable in ((EndMethod) command).Method.Variables)
                            {
                                if (variable is ByteVariable)
                                    ByteCounter--;
                                else
                                    DecreaseBitCounter();
                            }
                            break;
                        case CommandType.Return:
                        case CommandType.MethodCall:
                        case CommandType.And:
                        case CommandType.Not:
                        case CommandType.Or:
                        case CommandType.Add:
                        case CommandType.Subtract:
                        case CommandType.Multiply:
                        case CommandType.Divide:
                        case CommandType.Modulo:
                        case CommandType.Assignment:
                        case CommandType.VariableCall:
                        case CommandType.Bigger:
                        case CommandType.Smaller:
                        case CommandType.Equal:
                        case CommandType.UnEqual:
                            fin.AppendLine(command.ToString());
                            break;
                        case CommandType.Bool:
                            fin.AppendLine($"{((Bool) command).Name} bit {BitCounter}");
                            break;
                        case CommandType.Char:
                        case CommandType.Int:
                        case CommandType.Cint:
                            fin.AppendLine($"{((Variable) command).Name} data {ByteCounter}");
                            break;
                        case CommandType.Label: //TODO
                            fin.AppendLine($"{((Label) command).Name}:");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                    throw new Exception("Well Timo, you named your Classes differently to your Enum items.");
            }

            return fin.ToString();
        }
    }
}