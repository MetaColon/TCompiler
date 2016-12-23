﻿namespace TCompiler.Types
{
    public class IntPair
    {
        public int Item1;
        public int Item2;

        public IntPair(int item1, int item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public override string ToString() => $"{Item1}.{Item2}";
    }
}