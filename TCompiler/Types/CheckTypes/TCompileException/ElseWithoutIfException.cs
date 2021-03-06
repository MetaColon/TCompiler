﻿#region

using System;

using TCompiler.Types.CompilerTypes;

#endregion


namespace TCompiler.Types.CheckTypes.TCompileException
{
    /// <summary>
    ///     Gets thrown when the user has an else without an if around it
    /// </summary>
    [Serializable]
    public class ElseWithoutIfException : CompileException
    {
        /// <summary>
        ///     Initializes a new ElseWithoutIfException
        /// </summary>
        /// <param name="codeLineIndex">The line the exception got thrown</param>
        /// <param name="message">The message to show to the user</param>
        public ElseWithoutIfException (CodeLine codeLineIndex, string message = "Else cannot stand alone")
            : base (codeLineIndex, message) {}
    }
}