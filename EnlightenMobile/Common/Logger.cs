﻿using System;
using System.Text;
using System.IO;
using System.Globalization;

namespace EnlightenMobile
{
    public enum LogLevel { DEBUG, INFO, ERROR };

    public delegate void LogChangedDelegate();

    // copied from WasatchNET
    public class Logger 
    {
        ////////////////////////////////////////////////////////////////////////
        // Private attributes
        ////////////////////////////////////////////////////////////////////////

        static readonly Logger instance = new Logger();

        public StringBuilder history = null;
        private StreamWriter outfile;

        ////////////////////////////////////////////////////////////////////////
        // Public attributes
        ////////////////////////////////////////////////////////////////////////

        public LogLevel level { get; set; } = LogLevel.DEBUG;

        public bool liveUpdates;
        public LogChangedDelegate logChangedDelegate;

        static public Logger getInstance()
        {
            return instance;
        }

        public void setPathname(string path)
        {
            try
            {
                outfile = new StreamWriter(path);
                debug("log path set to {0}", path);
            }
            catch (Exception e)
            {
                error("Can't set log pathname: {0}", e);
            }
        }

        public bool debugEnabled()
        {
            return level <= LogLevel.DEBUG;
        }

        public bool error(string fmt, params Object[] obj)
        {
            if (level <= LogLevel.ERROR)
                log(LogLevel.ERROR, fmt, obj);

            return false; // convenient for many cases
        }

        public void info(string fmt, params Object[] obj)
        {
            if (level <= LogLevel.INFO)
                log(LogLevel.INFO, fmt, obj);
        }

        public void debug(string fmt, params Object[] obj)
        {
            if (level <= LogLevel.DEBUG)
                log(LogLevel.DEBUG, fmt, obj);
        }

        public void logString(LogLevel lvl, string msg)
        {
            if (level <= lvl)
                log(lvl, msg);
        }

        public void save(string pathname)
        {
            if (history is null)
            {
                Console.WriteLine("Can't save w/o history");
                return;
            }
           
            try
            {
                TextWriter tw = new StreamWriter(pathname);
                tw.Write(history);
                tw.Close();
            }
            catch (Exception e)
            {
                error("can't write {0}: {1}", pathname, e.Message);
            }
        }

        public void hexdump(byte[] buf, string prefix = "")
        {
            string line = "";
            for (int i = 0;  i < buf.Length; i++)
            {
                if (i % 16 == 0)
                {
                    if (i > 0)
                    {
                        debug("{0}0x{1}", prefix, line);
                        line = "";
                    }
                    line += String.Format("{0:x4}:", i);
                }
                line += String.Format(" {0:x2}", buf[i]);
            }
            if (line.Length > 0)
                debug("{0}0x{1}", prefix, line);
        }

        // log the first n elements of a labeled array 
        public void logArray(string label, double[] a, int n=5) 
        {
            StringBuilder s = new StringBuilder();
            if (a != null && a.Length > 0)
            {
                s.Append(string.Format("{0:f2}", a[0]));
                for (int i = 1; i < n; i++)
                    s.Append(string.Format(", {0:f2}", a[i]));
            }
            debug($"{label} [len {a.Length}]: {s}");
        }

        // Provided both so that internal log events can flow-up a screen update,
        // and also so that external tab switches can force an update.
        public void update() => logChangedDelegate?.Invoke();

        ////////////////////////////////////////////////////////////////////////
        // Private methods
        ////////////////////////////////////////////////////////////////////////

        private Logger()
        {
        }

        string getTimestamp()
        {
            // drop date, as Android phones have narrow screens
            return DateTime.Now.ToString("HH:mm:ss.fff: ", CultureInfo.InvariantCulture);
        }

        void log(LogLevel lvl, string fmt, params Object[] obj)
        {
            string msg = "[Wasatch] " + getTimestamp() + lvl + ": " + String.Format(fmt, obj);

            lock (instance)
            {
                Console.WriteLine(msg);

                if (outfile != null)
                {
                    outfile.WriteLine(msg);
                    outfile.Flush();
                }

                if (history != null)
                {
                    history.Append(msg + "\n");
                    if (liveUpdates)
                        update();
                }
            }
        }
    }
}