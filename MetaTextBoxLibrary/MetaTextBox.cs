﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global

namespace MetaTextBoxLibrary
{
    public class MetaTextBox : Control, ICloneable
    {
        private readonly List <int> _renderers = new List <int> ();

        private readonly HistoryCollection <Tuple <ColoredString, int>> _textHistory =
            new HistoryCollection <Tuple <ColoredString, int>> (100);

        private Bitmap _backgroundRenderedFrontEnd;

        private int?                 _characterWidth;
        private int                  _cursorX;
        private int                  _cursorY;
        private HScrollBar           _horizontalScrollBar;
        private List <ColoredString> _lines;

        private Point _mousePositionOnMouseDown;

        private bool _refreshingLines;
        private int  _rendererCount;
        private bool _rendering;
        private int  _selectionLength;
        private int  _startingCharacter;

        private int           _startingLine;
        private ColoredString _text;
        private VScrollBar    _verticalScrollBar;

        private const int LARGE_CHANGE = 10;

        private readonly bool _readOnly;

        private Font _font = new Font ("Consolas", 9.75F, FontStyle.Regular,
                                       GraphicsUnit.Point, 0);

        public MetaTextBox ()
        {
            InitializeComponent ();
            if (GetStringWidth ("i") != GetStringWidth ("m"))
                throw new Exception ("Only monospace fonts are valid!");
            Text = new ColoredString (new List <ColoredCharacter> ());
            AddToHistory ();
        }

        /// <inheritdoc />
        private MetaTextBox (
            int                  startingLine,             int    startingCharacter,
            int?                 characterWidth,           int    cursorX,                    int           cursorY,
            Point                mousePositionOnMouseDown, Bitmap backgroundRenderedFrontEnd, ColoredString text,
            bool                 refreshingLines,
            int                  rendererCount,     bool       rendering,
            VScrollBar           verticalScrollBar, HScrollBar horizontalScrollBar,
            List <ColoredString> lines,             int        selectionLength, bool readOnly,
            bool                 doubleBuffered,    int        tabSize)
        {
            _startingLine               = startingLine;
            _startingCharacter          = startingCharacter;
            _characterWidth             = characterWidth;
            _cursorX                    = cursorX;
            _cursorY                    = cursorY;
            _mousePositionOnMouseDown   = mousePositionOnMouseDown;
            _backgroundRenderedFrontEnd = backgroundRenderedFrontEnd;
            _text                       = text;
            _refreshingLines            = refreshingLines;
            _rendererCount              = rendererCount;
            _rendering                  = rendering;
            _verticalScrollBar          = verticalScrollBar;
            _horizontalScrollBar        = horizontalScrollBar;
            _lines                      = lines;
            _selectionLength            = selectionLength;
            _readOnly                   = readOnly;
            DoubleBuffered              = doubleBuffered;
            TabSize                     = tabSize;
        }

        /// <inheritdoc />
        public override Font Font
        {
            get => _font;
            set
            {
                _font = value;
                OnFontChanged (EventArgs.Empty);
            }
        }

        /// <inheritdoc />
        protected override void OnFontChanged (EventArgs e)
        {
            base.OnFontChanged (e);
            _characterWidth = GetStringWidth ("_");
            AsyncRefresh ();
        }

        /// <summary>
        ///     Always ends with \n
        /// </summary>
        [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
        public new ColoredString Text
        {
            get => _text;
            set
            {
                _text = value.Remove ('\r');
                _text = _text.LastOrDefault ()?.Character == '\n'
                            ? _text
                            : _text + new ColoredCharacter (ForeColor, '\n');
                RefreshLines ();
                _verticalScrollBar.Maximum   = Lines.Count - 2 + _verticalScrollBar.LargeChange - 1;
                _horizontalScrollBar.Maximum = GetMaxCharacterCount () - 2 + _horizontalScrollBar.LargeChange - 1;
            }
        }

        protected sealed override bool   DoubleBuffered { get; set; } = true;
        public override           Cursor Cursor         { get; set; } = Cursors.IBeam;
        private                   Color  SelectionColor { get; }      = Color.DodgerBlue;

        public int SelectionLength
        {
            get => _selectionLength;
            private set
            {
                _selectionLength = value;
                ColorSelectionInText ();
            }
        }

        private int TabSize { get; } = 4;

        public int CursorIndex
        {
            private set
            {
                var oldValue    = CursorIndex;
                var coordinates = GetCursorCoordinates (value);
                if (!coordinates.HasValue)
                    return;
                _cursorX = coordinates.Value.X;
                _cursorY = coordinates.Value.Y;
                RefreshCaretPosition (); //TODO refresh scrolling
                SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
            }
            get => GetCursorIndex (_cursorX, _cursorY);
        }

        [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
        public List <ColoredString> Lines
        {
            get
            {
                while (_refreshingLines) {}

                return _lines;
            }
            private set
            {
                _lines = value;
                RefreshCaretPosition ();
            }
        }

        /// <inheritdoc />
        public object Clone () => new MetaTextBox (_startingLine, _startingCharacter, _characterWidth, _cursorX,
                                                   _cursorY, _mousePositionOnMouseDown, _backgroundRenderedFrontEnd,
                                                   Text, _refreshingLines, _rendererCount, _rendering,
                                                   _verticalScrollBar, _horizontalScrollBar, _lines, _selectionLength,
                                                   _readOnly, DoubleBuffered,
                                                   TabSize);

        public void SetText (string text)
        {
            if (CursorIndex > text.Length - 1)
                CursorIndex = 0;
            if (SelectionLength + CursorIndex >= text.Length - 1)
                SelectionLength = 0;
            Text = new ColoredString (ForeColor, text);
            AddToHistory ();
        }

        public event EventHandler <SelectionChangedEventArgs> SelectionChanged;

        [SuppressMessage ("ReSharper", "InconsistentNaming")]
        private event EventHandler _textChanged;

        public new event EventHandler TextChanged
        {
            add => _textChanged += value;
            remove => _textChanged -= value;
        }

        private void InvokeTextChanged () => _textChanged?.Invoke (this, EventArgs.Empty);

        public event EventHandler OnScroll;

        public void SetCursorIndex (int value)
        {
            var oldValue = CursorIndex;
            CursorIndex = value;
            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
        }

        public int GetCharacterWidth ()
        {
            if (_characterWidth == null)
                _characterWidth = GetStringWidth ("_");
            return _characterWidth.Value;
        }


        public void InitializeComponent ()
        {
            _verticalScrollBar   = new VScrollBar ();
            _horizontalScrollBar = new HScrollBar ();
            SuspendLayout ();
            //
            // _horizontalScrollBar
            //
            _horizontalScrollBar.Name        =  "_horizontalScrollBar";
            _horizontalScrollBar.Cursor      =  DefaultCursor;
            _horizontalScrollBar.LargeChange =  _horizontalScrollBar.LargeChange / 2;
            _horizontalScrollBar.Scroll      += HorizontalScrollBarOnScroll;
            //
            // _verticalScrollBar
            //
            _verticalScrollBar.Name        =  "_verticalScrollBar";
            _verticalScrollBar.Cursor      =  DefaultCursor;
            _verticalScrollBar.LargeChange =  _verticalScrollBar.LargeChange / 2;
            _verticalScrollBar.Scroll      += VerticalScrollBarOnScroll;
            //
            // MetaTextBox
            //
            Controls.Add (_verticalScrollBar);
            Controls.Add (_horizontalScrollBar);
            ResumeLayout (false);
        }


        #region MetaFunctions

        public void ColorCharacter (int index, Color color, bool back = false)
        {
            if (back)
                Text.SetBackColor (index, color);
            else
                Text.SetForeColor (index, color);
            AsyncRefresh ();
        }

        public void ColorRange (int startingIndex, int count, Color color, bool back = false)
        {
            if (back)
                Text.
                    SetBackColorRange (
                        startingIndex,
                        count,
                        color);
            else
                Text.
                    SetForeColorRange (
                        startingIndex,
                        count,
                        color);
            AsyncRefresh ();
        }

        private void ColorRange (Point startingPosition, Point endPosition, Color color, bool back = false)
        {
            var startingIndex = GetCursorIndex (startingPosition.X, startingPosition.Y);
            var endIndex      = GetCursorIndex (endPosition.X, endPosition.Y);
            ColorRange (startingIndex, endIndex - startingIndex, color, back);
        }

        public void HighlightLine (int lineIndex, Color color) =>
            ColorRange (new Point (0, lineIndex), new Point (Lines [lineIndex].Count (), lineIndex), color, true);

        public string GetCurrentLine () => Lines.Count == 0 ? "" : Lines [_cursorY].ToString ();

        public List <int> GetSelectedLines () =>
            Enumerable.Range (CursorIndex, SelectionLength).Select (i => GetCursorCoordinates (i)?.Y).
                       Where (i => i.HasValue).Select (i => i.Value).Distinct ().
                       ToList ();

        public int GetLineFromCharIndex (int cursorIndex) =>
            GetCursorCoordinates (cursorIndex)?.Y ?? -1;

        public int GetFirstCharIndexFromLine (int line) =>
            GetCursorIndex (0, line);

        public int GetFirstCharIndexOfCurrentLine () => GetFirstCharIndexFromLine (_cursorY);

        public Point GetPositionFromCharIndex (int charIndex)
        {
            var cursorCoordinates = GetCursorCoordinates (charIndex);
            if (cursorCoordinates != null)
                return GetPointToCursorLocation (cursorCoordinates.Value);
            throw new IndexOutOfRangeException ($"Char index {charIndex} was out of bounds");
        }

        #endregion


        #region MainFunctions

        #region Handler

        protected override void OnKeyDown (KeyEventArgs e)
        {
            if (PerformInput (e.KeyCode, e) || PerformShortcut (e.KeyCode, e))
                e.Handled = true;
            else
                base.OnKeyDown (e);
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            e.Graphics.SmoothingMode     = SmoothingMode.HighSpeed;
            e.Graphics.InterpolationMode = InterpolationMode.Low;
            e.Graphics.PixelOffsetMode   = PixelOffsetMode.HighSpeed;
            if (_backgroundRenderedFrontEnd != null)
                e.Graphics.DrawImage (_backgroundRenderedFrontEnd, new Point (0, 0));
        }

        protected override void OnResize (EventArgs e)
        {
            AsyncRefresh ();
            _verticalScrollBar.Location   = new Point (Width - _verticalScrollBar.Width, 0);
            _verticalScrollBar.Size       = new Size (_verticalScrollBar.Width, Height);
            _horizontalScrollBar.Location = new Point (0, Height - _horizontalScrollBar.Height);
            _horizontalScrollBar.Size     = new Size (Width, _horizontalScrollBar.Height);
        }

        protected override void OnMouseWheel (MouseEventArgs e) //ENHANCEMENT: scroll horizontal on shift press
        {
            base.OnMouseWheel (e);
            if (Lines.Count <= 2)
                return;
            ScrollToHorizontal (_startingLine - e.Delta / Font.Height);
            _verticalScrollBar.Value = _startingLine *
                                       (_verticalScrollBar.Maximum -
                                        (_verticalScrollBar.LargeChange - 1) -
                                        _verticalScrollBar.Minimum) /
                                       (Lines.Count - 2);
        }

        /// <inheritdoc />
        protected override void OnMouseDown (MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                _mousePositionOnMouseDown = e.Location;
            Focus ();
            base.OnMouseDown (e);
        }

        /// <inheritdoc />
        protected override void OnMouseUp (MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                SetSelectionFromPosition (_mousePositionOnMouseDown, e.Location);
            base.OnMouseUp (e);
        }

        /// <inheritdoc />
        protected override void OnMouseMove (MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                SetSelectionFromPosition (_mousePositionOnMouseDown, e.Location);
            base.OnMouseMove (e);
        }

        private void VerticalScrollBarOnScroll (object sender, ScrollEventArgs scrollEventArgs) => ScrollToHorizontal (
            GetStartingLine (scrollEventArgs.NewValue));

        private void HorizontalScrollBarOnScroll (object sender, ScrollEventArgs scrollEventArgs) =>
            ScrollToVertical (GetStartingCharacter (scrollEventArgs.NewValue));

        protected override void OnGotFocus (EventArgs e)
        {
            CreateCaret (Handle, IntPtr.Zero, 1, Font.Height - 2);
            RefreshCaretPosition ();
            ShowCaret (Handle);
            base.OnGotFocus (e);
        }

        protected override void OnLostFocus (EventArgs e)
        {
            DestroyCaret ();
            base.OnLostFocus (e);
        }

        #endregion


        private void ScrollToHorizontal (int newStartingLine)
        {
            _startingLine = newStartingLine < 0
                                ? 0
                                : newStartingLine > Lines.Count - 2
                                    ? Lines.Count - 2
                                    : newStartingLine;
            RefreshCaretPosition ();
            AsyncRefresh ();
            OnScroll?.Invoke (this, EventArgs.Empty);
        }

        private void ScrollToVertical (int newStartingCharacter)
        {
            _startingCharacter = newStartingCharacter < 0
                                     ? 0
                                     : newStartingCharacter > GetMaxCharacterCount () - 1
                                         ? GetMaxCharacterCount () - 1
                                         : newStartingCharacter;
            RefreshCaretPosition ();
            AsyncRefresh ();
            OnScroll?.Invoke (this, EventArgs.Empty);
        }


        /// <summary>
        ///     DON'T CALL THIS! Call AsyncRefresh instead.
        /// </summary>
        public override void Refresh ()
        {
            if (_backgroundRenderedFrontEnd == null)
                return;
            base.Refresh ();
        }

        private void AsyncRefresh ()
        {
            Task.Factory.StartNew (async () =>
            {
                var index = _rendererCount;
                _rendererCount++;
                _renderers.Add (index);
                while (_rendering)
                    if (_renderers.Last () != index)
                        return;
                _renderers.Remove (index);
                _rendererCount--;
                _rendering                = true;
                Thread.CurrentThread.Name = "RenderingThread";
                while (!IsHandleCreated) {}

                _backgroundRenderedFrontEnd = await GetNewFrontend ();
                Invoke (new Action (Refresh));
                _rendering = false;
            }, TaskCreationOptions.None);
        }

        private async Task SyncRefresh ()
        {
            while (!IsHandleCreated) {}

            var index = _rendererCount;
            _renderers.Add (index);
            _rendererCount++;
            while (_rendering)
                if (_renderers.Last () != index)
                    return;
            _renderers.Remove (index);
            _rendererCount--;
            _rendering                  = true;
            _backgroundRenderedFrontEnd = await GetNewFrontend ();
            Invoke (new Action (Refresh));
            _rendering = false;
        }

        public void SetSelection (int start, int length)
        {
            var oldValue = CursorIndex;
            CursorIndex     = start;
            SelectionLength = length;
            AsyncRefresh ();
            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
        }

        private void SetSelection (Point startCursorLocation, Point endCursorLocation)
        {
            var startIndex = GetCursorIndex (startCursorLocation.X, startCursorLocation.Y);
            var endIndex   = GetCursorIndex (endCursorLocation.X, endCursorLocation.Y);
            SetSelection (endIndex, startIndex - endIndex);
        }

        private void SetSelectionFromPosition (Point startPosition, Point endPosition) =>
            SetSelection (GetCursorLocationToPoint (startPosition), GetCursorLocationToPoint (endPosition));

        #endregion


        #region HelperFunctions

        private int GetMaxCharacterCount () => Lines.Max (s => s.Count ());

        protected override bool IsInputKey (Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                case Keys.Shift | Keys.Right:
                case Keys.Shift | Keys.Left:
                case Keys.Shift | Keys.Up:
                case Keys.Shift | Keys.Down:
                case Keys.Tab:
                    return true;
            }

            return base.IsInputKey (keyData);
        }

        public Point GetCursorLocationToPoint (Point point)
        {
            var x = point.X / GetCharacterWidth () + _startingCharacter - GetLineNumberCharacterCount (Lines.Count);
            var y = point.Y / Font.Height + _startingLine;
            y = Lines.Count - 1 > y ? y : Lines.Count - 2;
            x = _lines [y].Count () > x ? x : _lines [y].Count () - 1;
            return new Point (x, y);
        }

        private Point GetPointToCursorLocation (Point cursorLocation) =>
            new Point ((cursorLocation.X -
                        _startingCharacter -
                        GetLineNumberCharacterCount (Lines.Count)) *
                       GetCharacterWidth (),
                       (cursorLocation.Y - _startingLine) * Font.Height);

        public void ColorSelectionInText ()
        {
            var cursorIndex     = CursorIndex;
            var selectionLength = SelectionLength;
            var selectionColor  = SelectionColor;
            for (var index = 0; index < Text.ColoredCharacters.Count; index++)
                if (cursorIndex <= index && cursorIndex + selectionLength > index ||
                    cursorIndex > index && cursorIndex + selectionLength <= index)
                    Text.ColoredCharacters [index].BackColor =
                        selectionColor;
                else
                    Text.ColoredCharacters [index].BackColor =
                        BackColor; //TODO extra backColor or something, selection splitted from other colors
        }

        private int GetStartingLine (int scrollBarValue) => (int) ((double) scrollBarValue /
                                                                   (_verticalScrollBar.Maximum -
                                                                    (_verticalScrollBar.LargeChange - 1) -
                                                                    _verticalScrollBar.Minimum) *
                                                                   Lines.Count);

        private int GetStartingCharacter (int scrollBarValue) => (int) ((double) scrollBarValue /
                                                                        (_horizontalScrollBar.Maximum -
                                                                         (_horizontalScrollBar.LargeChange - 1) -
                                                                         _horizontalScrollBar.Minimum) *
                                                                        GetMaxCharacterCount ());

        private void RefreshLines ()
        {
            _refreshingLines = true;
            Task.Factory.StartNew (async () =>
            {
                await Task.Run (() =>
                {
                    if (Text != null)
                        Lines = GetLines ();
                    _refreshingLines = false;
                });
                await SyncRefresh ();
            });
        }


        #region Shortcuts

        public bool PerformShortcut (Keys key, KeyEventArgs keyEventArgs)
        {
            if (!ValidateShortcut (keyEventArgs.Modifiers))
                return false;
            /**
            *
            * Normally valid keyboard shortcuts:
            * • CTRL + the key
            *
            **/

            switch (key)
            {
                case Keys.A:
                    SelectAll ();
                    return true;
                case Keys.C:
                    Copy ();
                    return true;
                case Keys.X:
                    if (_readOnly)
                        Copy ();
                    else
                        Cut ();
                    return true;
                case Keys.V:
                    if (_readOnly)
                        return false;
                    Paste ();
                    return true;
                case Keys.Z:
                    if (_readOnly)
                        return false;
                    Undo ();
                    return true;
                case Keys.Y:
                    if (_readOnly)
                        return false;
                    Redo ();
                    return true;
                default:
                    return false;
            }
        }

        public void SelectAll () =>
            SetSelection (Text.Count () - 1, -(Text.Count () - 1));

        public void Copy ()
        {
            var selectedText = GetSelectedText ();
            if (string.IsNullOrEmpty (selectedText))
                return;
            Clipboard.SetText (selectedText);
        }

        public void Cut ()
        {
            Copy ();
            DeleteSelection ();
            AddToHistory ();
        }

        public void Paste () => InsertText (Clipboard.ContainsText () ? Clipboard.GetText () : "");

        public void Undo ()
        {
            var undone = _textHistory.Undo ();
            if (undone == null)
                return;
            _text = undone.Item1;
            var oldValue = CursorIndex;
            CursorIndex     = undone.Item2;
            SelectionLength = 0;
            RefreshLines ();
            _verticalScrollBar.Maximum   = Lines.Count - 2 + _verticalScrollBar.LargeChange - 1;
            _horizontalScrollBar.Maximum = GetMaxCharacterCount () - 2 + _horizontalScrollBar.LargeChange - 1;
            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
        }

        public void Redo ()
        {
            var redone = _textHistory.Redo ();
            if (redone == null)
                return;
            _text           = redone.Item1;
            SelectionLength = 0;
            RefreshLines ();
            var oldValue = CursorIndex;
            CursorIndex                  = redone.Item2;
            _verticalScrollBar.Maximum   = Lines.Count - 2 + _verticalScrollBar.LargeChange - 1;
            _horizontalScrollBar.Maximum = GetMaxCharacterCount () - 2 + _horizontalScrollBar.LargeChange - 1;
            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
        }

        private void AddToHistory ()
        {
            _textHistory.Push (new Tuple <ColoredString, int> (_text, CursorIndex));
            InvokeTextChanged ();
        }

        #endregion


        public void InsertText (string text)
        {
            int oldValue;
            if (SelectionLength == 0)
            {
                Text = Text.Insert (
                    CursorIndex,
                    new ColoredString (ForeColor, text));
                oldValue    =  CursorIndex;
                CursorIndex += text.Length;
                AddToHistory ();
            }
            else
            {
                var start  = SelectionLength > 0 ? CursorIndex : CursorIndex + SelectionLength;
                var length = Math.Abs (SelectionLength);
                Text = Text.Replace (
                    start, length,
                    new ColoredString (ForeColor, text));
                SelectionLength = 0;
                oldValue        = CursorIndex;
                CursorIndex     = start + text.Length;
                AddToHistory ();
            }

            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
        }

        public string GetSelectedText ()
        {
            var beginning = CursorIndex + (_selectionLength > 0 ? 0 : _selectionLength);
            var length    = Math.Abs (SelectionLength);
            return Text.GetRange (beginning, length).ToString ();
        }


        public bool PerformInput (Keys key, KeyEventArgs keyEventArgs)
        {
            if (!ValidateInput (keyEventArgs.Modifiers))
                return false;
            /**
             *
             * Normally valid keyboard inputs:
             * • The key
             * • SHIFT + the key
             * • ALT + CTRL + the key
             *
             **/

            var keyInput = KeyInput.AllKeyInputs.
                                    Where (input => input.Key == key).
                                    Select (input => input.GetCharacter (
                                                keyEventArgs.Shift || IsKeyLocked (Keys.CapsLock), keyEventArgs.Control,
                                                keyEventArgs.Alt)).
                                    FirstOrDefault (c => c != null);
            if (keyInput != null)
            {
                if (_readOnly)
                    return false;
                var oldIndex = CursorIndex;
                if (keyInput.Value == '\t')
                {
                    InsertText (new string (' ', TabSize));
                    CursorIndex = oldIndex + 4;
                    AddToHistory ();
                }
                else
                {
                    InsertText (keyInput.Value.ToString ());
                    CursorIndex = oldIndex + 1;
                    AddToHistory ();
                }

                SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldIndex, CursorIndex));
                return true;
            }

            switch (key)
            {
                case Keys.Back: //TODO selection deletion tests
                    if (_readOnly)
                        return false;
                    if (SelectionLength == 0 && CursorIndex > 0)
                    {
                        var oldIndex = CursorIndex;
                        DeleteCharacter (CursorIndex - 1);
                        CursorIndex = oldIndex - 1;
                        SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldIndex, CursorIndex));
                    }
                    else if (SelectionLength != 0)
                        DeleteSelection ();
                    else
                        return false;

                    AddToHistory ();
                    return true;
                case Keys.Delete:
                    if (_readOnly)
                        return false;
                    if (CursorIndex < Text.Count () - 1 && SelectionLength == 0)
                        DeleteCharacter (CursorIndex);
                    else if (SelectionLength != 0)
                        DeleteSelection ();
                    else
                        return false;
                    AddToHistory ();
                    return true;
                case Keys.Down:
                {
                    var end      = _cursorY >= Lines.Count - 1;
                    var oldIndex = CursorIndex;
                    if (!end)
                    {
                        var startingCursorPosition = CursorIndex;
                        CursorIndex += -_cursorX +
                                       Lines [_cursorY].Count () +
                                       (_cursorX < Lines [_cursorY + 1].Count ()
                                            ? _cursorX
                                            : Lines [_cursorY + 1].Count () - 1
                                       ); //To the beginning -> to the next line -> restore x position
                        if (keyEventArgs.Shift)
                            SetSelection (CursorIndex, SelectionLength + (startingCursorPosition - CursorIndex));
                        else
                            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldIndex, CursorIndex));
                    }

                    if (!keyEventArgs.Shift)
                    {
                        SelectionLength = 0;
                        if (end)
                            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldIndex, CursorIndex));
                    }

                    AsyncRefresh ();
                    return true;
                }
                case Keys.Up:
                {
                    var oldValue = CursorIndex;
                    var top      = _cursorY <= 0;
                    if (!top)
                    {
                        var startingCursorPosition = CursorIndex;
                        CursorIndex += -_cursorX -
                                       Lines [_cursorY - 1].Count () +
                                       (_cursorX < Lines [_cursorY - 1].Count ()
                                            ? _cursorX
                                            : Lines [_cursorY - 1].Count () - 1
                                       ); //To the beginning -> to the previous line -> restore x position
                        if (keyEventArgs.Shift)
                            SetSelection (CursorIndex, SelectionLength + (startingCursorPosition - CursorIndex));
                        else
                            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
                    }

                    if (!keyEventArgs.Shift)
                    {
                        SelectionLength = 0;
                        if (top)
                            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
                    }

                    AsyncRefresh ();
                    return true;
                }
                case Keys.Left:
                {
                    var oldValue = CursorIndex;
                    if (CursorIndex > 0)
                    {
                        CursorIndex--;
                        if (keyEventArgs.Shift)
                            SelectionLength++;
                    }

                    if (!keyEventArgs.Shift)
                        SelectionLength = 0;
                    AsyncRefresh ();
                    SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
                    return true;
                }
                case Keys.Right:
                {
                    var oldValue = CursorIndex;
                    if (CursorIndex < Text.Count () - 1)
                    {
                        CursorIndex++;
                        if (keyEventArgs.Shift)
                            SelectionLength--;
                    }

                    if (!keyEventArgs.Shift)
                        SelectionLength = 0;
                    AsyncRefresh ();
                    SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldValue, CursorIndex));
                    return true;
                }
                case Keys.End:
                {
                    var oldCursorIndex = CursorIndex;
                    SetCursorPosition (
                        Lines.Count > 0 ? Lines [_cursorY].Count () > 0 ? Lines [_cursorY].Count () - 1 : 0 : _cursorX,
                        _cursorY);
                    if (!keyEventArgs.Shift)
                        SelectionLength = 0;
                    else
                        SelectionLength += oldCursorIndex - CursorIndex;
                    AsyncRefresh ();
                    SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldCursorIndex, CursorIndex));
                    return true;
                }
                case Keys.Home:
                {
                    var oldCursorIndex = CursorIndex;
                    SetCursorPosition (0, _cursorY);
                    if (!keyEventArgs.Shift)
                        SelectionLength = 0;
                    else
                        SelectionLength += oldCursorIndex - CursorIndex;
                    AsyncRefresh ();
                    SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldCursorIndex, CursorIndex));
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool ValidateInput (Keys modifiers) => modifiers == Keys.None ||
                                                              modifiers == Keys.Shift ||
                                                              modifiers == (Keys.Control | Keys.Alt);

        private static bool ValidateShortcut (Keys modifiers) => modifiers == Keys.Control;

        private void DeleteCharacter (int index)
        {
            Text = Text.Remove (index, 1);
        }

        private void InsertCharacter (int index, ColoredCharacter coloredCharacter) =>
            Text = Text.Insert (index, coloredCharacter);

        private void InsertString (int index, ColoredString coloredString) =>
            Text = Text.Insert (index, coloredString);

        private void SetCursorPosition (int x, int y)
        {
            _cursorX = x;
            _cursorY = y;
            RefreshCaretPosition ();
        }

        private void SetCursorPosition (Point position) => SetCursorPosition (position.X, position.Y);

        private void RefreshCaretPosition () =>
            SetCaretPosition (_cursorX - _startingCharacter, _cursorY - _startingLine);

        private void SetCaretPosition (int x, int y)
        {
            SetCaretPos (
                3 + GetCharacterWidth () * (x + GetLineNumberCharacterCount (_lines.Count)),
                1 + y * Font.Height);
        }

        private async Task <Bitmap> GetNewFrontend () => await Task.Run (() =>
        {
            if (Size.Width <= 0 || Size.Height <= 0)
                return null;
            var bitmap =
                new Bitmap (Size.Width, Size.Height);

            var lines = new List <ColoredString> (Lines);

            var drawableLines =
                new List <DrawableLine> ();

            var lineNumbers = GetLineNumbers (lines.Count);
            var currentPoint =
                new Point (0, 0);

            for (var index = _startingLine; index < lines.Count; index++)
            {
                var line = lines [index];
                if (currentPoint.Y - Location.Y >
                    Size.Height)
                    break;
                drawableLines.Add (new DrawableLine
                {
                    LineRanges =
                        new List <ColoredString>
                            {
                                new ColoredString (ForeColor, lineNumbers [index])
                            }.Concat (
                                  _startingCharacter <=
                                  line.
                                      Count ()
                                      ? GetLineRanges (line.
                                                           Substring (_startingCharacter))
                                      : new
                                          List <ColoredString> ()).
                              ToList (),
                    Location = currentPoint
                });
                currentPoint.Y += Font.Height;
            }

            DrawLinesToImage (bitmap, drawableLines, Font, BackColor);

            return bitmap;
        });

        private static List <string> GetLineNumbers (int lineCount)
        {
            var lineNumberWidth = GetLineNumberCharacterCount (lineCount);
            var allNumbers = Enumerable.Range (1, lineCount).
                                        Select (i => $"{i} ".PadLeft (lineNumberWidth, ' ')).ToList ();
            allNumbers [allNumbers.Count - 1] = string.Empty.PadLeft (lineNumberWidth, ' ');
            return allNumbers;
        }

        private static int GetLineNumberCharacterCount (int lineCount) => (int) (Math.Log (lineCount, 10) + 2);

        public static List <ColoredString> GetLineRanges (ColoredString line)
        {
            var fin              = new List <ColoredString> ();
            var currentForeColor = line.GetFirstOrDefaultForeColor ();
            var currentBackColor = line.GetFirstOrDefaultBackColor ();
            var currentRange     = new ColoredString (new List <ColoredCharacter> ());
            foreach (var coloredCharacter in line.ColoredCharacters)
            {
                if (!char.IsWhiteSpace (coloredCharacter.Character) &&
                    (currentForeColor == null ||
                     currentForeColor.Value != coloredCharacter.ForeColor) ||
                    currentBackColor == null ||
                    currentBackColor.Value != coloredCharacter.BackColor)
                {
                    if (currentRange.Count () > 0)
                        fin.Add (currentRange);
                    currentRange     = new ColoredString (new List <ColoredCharacter> ());
                    currentBackColor = coloredCharacter.BackColor;
                    currentForeColor = coloredCharacter.ForeColor;
                }

                currentRange += coloredCharacter;
            }

            if (currentRange.Count () > 0)
                fin.Add (currentRange);
            return fin;
        }

        private List <ColoredString> GetLines () => Text.Split ('\n', true).ToList ();

        private Point? GetCursorCoordinates (int cursorIndex)
        {
            var leftCursorPosition = cursorIndex;
            for (var i = 0; i < Lines.Count; i++)
            {
                var newCursorPoint = leftCursorPosition - Lines [i].Count ();
                if (newCursorPoint < 0)
                    return new Point (leftCursorPosition, i);
                leftCursorPosition = newCursorPoint;
            }

            return null;
        }

        private int GetCursorIndex (int cursorX, int cursorY)
        {
            var fin = cursorX;
            while (_refreshingLines) {}

            for (var l = cursorY - 1; l >= 0; l--)
                fin += Lines [l].Count ();
            return fin;
        }

        public int GetStringWidth (string s, Graphics g = null)
        {
            var graphics = g ?? CreateGraphics ();
            return TextRenderer.MeasureText (graphics, s, Font).Width -
                   (s.Length > 0 ? TextRenderer.MeasureText (graphics, "_", Font).Width / 2 : 0);
        }

        private void DrawLinesToImage (
            Image image, List <DrawableLine> lines, Font font, Color backColor = default (Color))
        {
            var memoryHdc = CreateMemoryHdc (IntPtr.Zero, image.Width, image.Height, out IntPtr dib);
            try
            {
                using (var memoryGraphics = Graphics.FromHdc (memoryHdc))
                {
                    memoryGraphics.Clear (backColor);
                    foreach (var drawableLine in lines)
                    {
                        var currentLocation = new Point (drawableLine.Location.X, drawableLine.Location.Y);
                        foreach (var drawableLineRange in drawableLine.LineRanges)
                        {
                            var foreColor     = drawableLineRange.GetFirstOrDefaultForeColor ();
                            var textBackColor = drawableLineRange.GetFirstOrDefaultBackColor ();
                            if (foreColor == null || textBackColor == null)
                                throw new Exception ("Variable shouldn't be null");
                            Console.WriteLine (drawableLineRange.Remove ('\n').ToString ());
                            TextRenderer.DrawText (memoryGraphics, drawableLineRange.Remove ('\n').ToString (),
                                                   font,
                                                   currentLocation,
                                                   foreColor.Value,
                                                   (Color) textBackColor,
                                                   TextFormatFlags.Default | TextFormatFlags.NoPrefix);
                            currentLocation.X += drawableLineRange.Count () * GetCharacterWidth ();
                        }
                    }
                }

                using (var imageGraphics = Graphics.FromImage (image))
                {
                    var imgHdc = imageGraphics.GetHdc ();
                    BitBlt (imgHdc, 0, 0, image.Width, image.Height, memoryHdc, 0, 0, 0x00CC0020);
                    imageGraphics.ReleaseHdc (imgHdc);
                }
            }
            finally
            {
                DeleteObject (dib);
                DeleteDC (memoryHdc);
            }
        }

        private void DeleteSelection ()
        {
            var oldCursorIndex     = CursorIndex + (SelectionLength > 0 ? 0 : SelectionLength);
            var oldSelectionLength = Math.Sign (SelectionLength) * SelectionLength;
            CursorIndex     += SelectionLength > 0 ? 0 : SelectionLength;
            SelectionLength =  0;
            Text            =  Text.Remove (oldCursorIndex, oldSelectionLength);
            SelectionChanged?.Invoke (this, new SelectionChangedEventArgs (oldCursorIndex, CursorIndex));
        }

        #endregion


        #region ImportedMethods

        private static IntPtr CreateMemoryHdc (IntPtr hdc, int width, int height, out IntPtr dib)
        {
            var memoryHdc = CreateCompatibleDC (hdc);
            SetBkMode (memoryHdc, 1);

            var info = new BitMapInfo ();
            info.biSize        = Marshal.SizeOf (info);
            info.biWidth       = width;
            info.biHeight      = -height;
            info.biPlanes      = 1;
            info.biBitCount    = 32;
            info.biCompression = 0; // BI_RGB
            dib                = CreateDIBSection (hdc, ref info, 0, out IntPtr _, IntPtr.Zero, 0);
            SelectObject (memoryHdc, dib);

            return memoryHdc;
        }

        [DllImport ("gdi32.dll")]
        private static extern int SetBkMode (IntPtr hdc, int mode);

        [DllImport ("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC (IntPtr hdc);

        [DllImport ("gdi32.dll")]
        private static extern IntPtr CreateDIBSection (
            IntPtr     hdc,     [In] ref BitMapInfo pbmi,     uint iUsage,
            out IntPtr ppvBits, IntPtr              hSection, uint dwOffset);

        [DllImport ("gdi32.dll")]
        private static extern int SelectObject (IntPtr hdc, IntPtr hgdiObj);

        [DllImport ("gdi32.dll")]
        [return: MarshalAs (UnmanagedType.Bool)]
        private static extern bool BitBlt (
            IntPtr hdc,   int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
            int    nXSrc, int nYSrc,  int dwRop);

        [DllImport ("gdi32.dll")]
        private static extern bool DeleteObject (IntPtr hObject);

        [DllImport ("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC (IntPtr hdc);


        [StructLayout (LayoutKind.Sequential)]
        private struct BitMapInfo
        {
            public           int   biSize;
            public           int   biWidth;
            public           int   biHeight;
            public           short biPlanes;
            public           short biBitCount;
            public           int   biCompression;
            private readonly int   biSizeImage;
            private readonly int   biXPelsPerMeter;
            private readonly int   biYPelsPerMeter;
            private readonly int   biClrUsed;
            private readonly int   biClrImportant;
            private readonly byte  bmiColors_rgbBlue;
            private readonly byte  bmiColors_rgbGreen;
            private readonly byte  bmiColors_rgbRed;
            private readonly byte  bmiColors_rgbReserved;
        }


        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool CreateCaret (IntPtr hWnd, IntPtr hBmp, int w, int h);

        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool SetCaretPos (int x, int y);

        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool ShowCaret (IntPtr hWnd);

        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool DestroyCaret ();

        #endregion
    }
}