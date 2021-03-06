﻿/*
 * https://github.com/tonerdo/readline/blob/master/LICENSE
 * The MIT License(MIT)
 * 
 * Copyright(c) 2017 Toni Solarin-Sodara
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace SWSH {
    public static class ReadLine {
        private static KeyHandler _keyHandler;
        public static List<string> History;
        public static List<string> GetHistory() => History;
        public static Func<string, int, string[]> AutoCompletionHandler { private get; set; }
        public static bool PasswordMode { private get; set; }
        public static string Read(string prompt = "", string defaultInput = "") {
            Console.Write(prompt);

            _keyHandler = new KeyHandler(new Console2() { PasswordMode = PasswordMode }, History, AutoCompletionHandler);
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);

            while (keyInfo.Key != ConsoleKey.Enter) {
                _keyHandler.Handle(keyInfo);
                keyInfo = Console.ReadKey(true);
            }

            Console.WriteLine();

            string text = _keyHandler.Text;
            if (String.IsNullOrWhiteSpace(text) && !String.IsNullOrWhiteSpace(defaultInput))
                text = defaultInput;
            else
                History.Add(text);

            return text;
        }
    }
    internal class KeyHandler {
        private int _cursorPos;
        private int _cursorLimit;
        private readonly StringBuilder _text;
        private readonly List<string> _history;
        private int _historyIndex;
        private ConsoleKeyInfo _keyInfo;
        private readonly Dictionary<string, Action> _keyActions;
        private string[] _completions;
        private int _completionStart;
        private int _completionsIndex;
        private readonly IConsole _console2;

        private bool IsStartOfLine() => _cursorPos == 0;
        private bool IsEndOfLine() => _cursorPos == _cursorLimit;
        private bool IsStartOfBuffer() => _console2.CursorLeft == 0;
        private bool IsEndOfBuffer() => _console2.CursorLeft == _console2.BufferWidth - 1;
        private bool IsInAutoCompleteMode() => _completions != null;
        private void MoveCursorLeft() {
            if (IsStartOfLine())
                return;
            if (IsStartOfBuffer())
                _console2.SetCursorPosition(_console2.BufferWidth - 1, _console2.CursorTop - 1);
            else
                _console2.SetCursorPosition(_console2.CursorLeft - 1, _console2.CursorTop);
            _cursorPos--;
        }
        private void MoveCursorHome() {
            while (!IsStartOfLine())
                MoveCursorLeft();
        }
        private string BuildKeyInput() {
            return (_keyInfo.Modifiers != ConsoleModifiers.Control && _keyInfo.Modifiers != ConsoleModifiers.Shift) ?
                _keyInfo.Key.ToString() : _keyInfo.Modifiers.ToString() + _keyInfo.Key.ToString();
        }
        private void MoveCursorRight() {
            if (IsEndOfLine())
                return;
            if (IsEndOfBuffer())
                _console2.SetCursorPosition(0, _console2.CursorTop + 1);
            else
                _console2.SetCursorPosition(_console2.CursorLeft + 1, _console2.CursorTop);
            _cursorPos++;
        }
        private void MoveCursorEnd() {
            while (!IsEndOfLine())
                MoveCursorRight();
        }
        private void ClearLine() {
            MoveCursorEnd();
            while (!IsStartOfLine())
                Backspace();
        }
        private void WriteNewString(string str) {
            ClearLine();
            foreach (char character in str)
                WriteChar(character);
        }
        private void WriteString(string str) {
            foreach (char character in str)
                WriteChar(character);
        }
        private void WriteChar() => WriteChar(_keyInfo.KeyChar);
        private void WriteChar(char c) {
            if (IsEndOfLine()) {
                _text.Append(c);
                _console2.Write(c.ToString());
                _cursorPos++;
            } else {
                int left = _console2.CursorLeft;
                int top = _console2.CursorTop;
                string str = _text.ToString().Substring(_cursorPos);
                _text.Insert(_cursorPos, c);
                _console2.Write(c.ToString() + str);
                _console2.SetCursorPosition(left, top);
                MoveCursorRight();
            }
            _cursorLimit++;
        }
        private void Backspace() {
            if (IsStartOfLine())
                return;
            MoveCursorLeft();
            int index = _cursorPos;
            _text.Remove(index, 1);
            string replacement = _text.ToString().Substring(index);
            int left = _console2.CursorLeft;
            int top = _console2.CursorTop;
            _console2.Write($"{replacement} ");
            _console2.SetCursorPosition(left, top);
            _cursorLimit--;
        }
        private void StartAutoComplete() {
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex = 0;

            WriteString(_completions[_completionsIndex]);
        }
        private void NextAutoComplete() {
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex++;

            if (_completionsIndex == _completions.Length)
                _completionsIndex = 0;

            WriteString(_completions[_completionsIndex]);
        }
        private void PreviousAutoComplete() {
            while (_cursorPos > _completionStart)
                Backspace();

            _completionsIndex--;

            if (_completionsIndex == -1)
                _completionsIndex = _completions.Length - 1;

            WriteString(_completions[_completionsIndex]);
        }
        private void PrevHistory() {
            if (_historyIndex > 0) {
                _historyIndex--;
                WriteNewString(_history[_historyIndex]);
            }
        }
        private void NextHistory() {
            if (_historyIndex < _history.Count) {
                _historyIndex++;
                if (_historyIndex == _history.Count)
                    ClearLine();
                else
                    WriteNewString(_history[_historyIndex]);
            }
        }
        private void ResetAutoComplete() {
            _completions = null;
            _completionsIndex = 0;
        }
        public string Text => _text.ToString();
        public KeyHandler(IConsole console, List<string> history, Func<string, int, string[]> autoCompleteHandler) {
            _console2 = console;

            _historyIndex = history.Count;
            _history = history;
            _text = new StringBuilder();
            _keyActions = new Dictionary<string, Action> {
                ["LeftArrow"] = MoveCursorLeft,
                ["Home"] = MoveCursorHome,
                ["End"] = MoveCursorEnd,
                ["ControlA"] = MoveCursorHome,
                ["ControlB"] = MoveCursorLeft,
                ["RightArrow"] = MoveCursorRight,
                ["ControlF"] = MoveCursorRight,
                ["ControlE"] = MoveCursorEnd,
                ["Backspace"] = Backspace,
                ["ControlH"] = Backspace,
                ["ControlL"] = ClearLine,
                ["UpArrow"] = PrevHistory,
                ["ControlP"] = PrevHistory,
                ["DownArrow"] = NextHistory,
                ["ControlN"] = NextHistory,
                ["ControlU"] = () => {
                    while (!IsStartOfLine())
                        Backspace();
                },
                ["ControlK"] = () => {
                    int pos = _cursorPos;
                    MoveCursorEnd();
                    while (_cursorPos > pos)
                        Backspace();
                },
                ["ControlW"] = () => {
                    while (!IsStartOfLine() && _text[_cursorPos - 1] != ' ')
                        Backspace();
                },

                ["Tab"] = () => {
                    if (IsInAutoCompleteMode()) {
                        NextAutoComplete();
                    } else {
                        if (autoCompleteHandler == null || !IsEndOfLine())
                            return;

                        char[] anyOf = new char[] { ' ', '.', '/', '\\', ':' };
                        string text = _text.ToString();

                        _completionStart = text.LastIndexOfAny(anyOf);
                        _completionStart = _completionStart == -1 ? 0 : _completionStart + 1;

                        _completions = autoCompleteHandler.Invoke(text, _completionStart);
                        _completions = _completions?.Length == 0 ? null : _completions;

                        if (_completions == null)
                            return;

                        StartAutoComplete();
                    }
                },

                ["ShiftTab"] = () => {
                    if (IsInAutoCompleteMode()) {
                        PreviousAutoComplete();
                    }
                }
            };
        }
        public void Handle(ConsoleKeyInfo keyInfo) {
            _keyInfo = keyInfo;

            // If in auto complete mode and Tab wasn't pressed
            if (IsInAutoCompleteMode() && _keyInfo.Key != ConsoleKey.Tab)
                ResetAutoComplete();

            _keyActions.TryGetValue(BuildKeyInput(), out Action action);
            action = action ?? WriteChar;
            action.Invoke();
        }
    }
    internal interface IConsole {
        int CursorLeft { get; }
        int CursorTop { get; }
        int BufferWidth { get; }
        int BufferHeight { get; }
        void SetCursorPosition(int left, int top);
        void SetBufferSize(int width, int height);
        void Write(string value);
        void WriteLine(string value);
    }
    internal class Console2 : IConsole {
        public int CursorLeft => Console.CursorLeft;
        public int CursorTop => Console.CursorTop;
        public int BufferWidth => Console.BufferWidth;
        public int BufferHeight => Console.BufferHeight;
        public bool PasswordMode { get; set; }
        public void SetBufferSize(int width, int height) => Console.SetBufferSize(width, height);
        public void SetCursorPosition(int left, int top) {
            if (!PasswordMode)
                Console.SetCursorPosition(left, top);
        }
        public void Write(string value) {
            if (PasswordMode)
                value = new String(default(char), value.Length);
            Console.Write(value);
        }
        public void WriteLine(string value) => Console.WriteLine(value);
    }
}
