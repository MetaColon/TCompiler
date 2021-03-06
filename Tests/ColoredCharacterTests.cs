﻿using System;
using System.Drawing;

using MetaTextBoxLibrary;

using NUnit.Framework;


namespace Tests
{
    [TestFixture]
    public class ColoredCharacterTests
    {
        [Test]
        public void ColoredCharacterTest ()
        {
            try
            {
                // ReSharper disable once ObjectCreationAsStatement
                new ColoredCharacter (Color.AliceBlue, Color.AliceBlue, ' ');
            }
            catch (Exception e)
            {
                if (e is AssertionException)
                    throw;
                Assert.Fail (e.Message);
            }
        }

        [Test]
        public void EqualsTest ()
        {
            try
            {
                var sUt = new ColoredCharacter (Color.AliceBlue, Color.AliceBlue, ' ');
                var c1  = new ColoredCharacter (Color.AliceBlue, Color.AliceBlue, ' ');
                var c2  = new ColoredCharacter (Color.AntiqueWhite, Color.AntiqueWhite, ' ');
                var c3  = new ColoredCharacter (Color.AliceBlue, Color.AliceBlue, '_');
                var c4  = new ColoredCharacter (Color.AliceBlue, Color.AntiqueWhite, ' ');
                var n1  = Equals (sUt, c1);
                var n2  = Equals (sUt, c2);
                var n3  = Equals (sUt, c3);
                var n4  = Equals (sUt, c4);
                Assert.IsTrue (n1);
                Assert.IsFalse (n2);
                Assert.IsFalse (n3);
                Assert.IsFalse (n4);
            }
            catch (Exception e)
            {
                if (e is AssertionException)
                    throw;
                Assert.Fail (e.Message);
            }
        }
    }
}