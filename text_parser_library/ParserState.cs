using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    public class ParserState
    {
        private States _currentState;
        private int? _lineNumber;
        private string _message;
        public enum States
        {
            Waiting,
            Working,
            Error
        }
        public int? LineNumber
        {
            get { return _lineNumber; }
            private set
            {
                if (_lineNumber != value)
                {
                    _lineNumber = value;
                    OnLineNumberChanged(EventArgs.Empty);
                }
            }
        }

        public States CurrentState
        {
            get { return _currentState; }
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnStateChanged(EventArgs.Empty);
                }
            }
        }
        public string Message
        {
            get { return _message; }
            private set
            {
                if (_message != value)
                {
                    _message = value;
                    OnMessageChanged(EventArgs.Empty);
                }
            }
        }

        public event EventHandler StateChanged;
        public event EventHandler LineNumberChanged;
        public event EventHandler MessageChanged;


        public ParserState()
        {
            LineNumber = null;
            CurrentState = States.Waiting;
        }

        public void UpdateLineNumber(int lineNumb)
        {
            LineNumber = lineNumb;
        }

        public void SetState(States newState)
        {
            CurrentState = newState;
        }

        public void Reset()
        {
            LineNumber = null;
            CurrentState = States.Waiting;
            ClearMessage();
        }

        public void AddMessage(string message)
        {
            Message += $"\n{message}";
        }
        public void ClearMessage()
        {
            Message = "";
        }

        private void OnStateChanged(EventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        private void OnLineNumberChanged(EventArgs e)
        {
            LineNumberChanged?.Invoke(this, e);
        }

        private void OnMessageChanged(EventArgs e)
        {
            MessageChanged?.Invoke(this, e);
        }

    }
}
