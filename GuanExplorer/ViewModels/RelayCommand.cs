// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Windows.Input;

namespace DatalogExplorer.ViewModels
{
    public class RelayCommand : ICommand
    {
        readonly Action<object?>? _execute;
        readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute) : this(execute, null) { }
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        [DebuggerStepThrough]
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object? parameter) { if (_execute != null) _execute(parameter); }
    }
}

