using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StationVoice.Core
{
    /// <summary>线程安全的简单日志容器，供 UI 与 CLR 两端共用。</summary>
    public class LogBus
    {
        private readonly List<string> _lines = new List<string>();
        private readonly object _gate = new object();

        public event EventHandler Changed;

        public void Info(string message) { Add("INFO", message); }
        public void Warn(string message) { Add("WARN", message); }
        public void Error(string message) { Add("ERR ", message); }

        public void Add(string level, string message)
        {
            string line = string.Format(CultureInfo.InvariantCulture,
                "[{0}] {1} {2}", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), level, message);
            lock (_gate) { _lines.Add(line); }
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public string Text
        {
            get
            {
                lock (_gate) { return string.Join(Environment.NewLine, _lines.ToArray()); }
            }
        }

        public string[] Lines
        {
            get { lock (_gate) { return _lines.ToArray(); } }
        }

        public void Clear()
        {
            lock (_gate) { _lines.Clear(); }
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
