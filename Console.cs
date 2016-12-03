//
// System.Console.cs
//
// Author:
// 	Dietmar Maurer (dietmar@ximian.com)
//		Gonzalo Paniagua Javier (gonzalo@ximian.com)
//		Neil Colvin
//
// (C) Ximian, Inc.  http://www.ximian.com
// (C) 2004,2005 Novell, Inc. (http://www.novell.com)
// Copyright 2013 Xamarin Inc. (http://www.xamarin.com)
// © 2016 Nivloc Enterprises Ltd
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
#if SSHARP
using System.Collections.Generic;
using Crestron.SimplSharp;
using SSMono.IO;
using TextWriter = SSMono.IO.TextWriter;
using TextReader = SSMono.IO.TextReader;
using StreamWriter = SSMono.IO.StreamWriter;
#if UNC
using Stream = SSMono.IO.Stream;
#else
using Stream = Crestron.SimplSharp.CrestronIO.Stream;
#endif
using SSMono.Security.Permissions;
using GC = Crestron.SimplSharp.CrestronEnvironment.GC;
#else
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
#endif
using System.Runtime.CompilerServices;
using System.Text;
using System.Globalization;

#if SSHARP
namespace SSMono
#else
namespace System
#endif
	{
	public static partial class Console
		{
#if !NET_2_1
		private class WindowsConsole
			{
#if CONSOLEIN
			public static bool ctrlHandlerAdded = false;

			private delegate bool WindowsCancelHandler (int keyCode);

			private static WindowsCancelHandler cancelHandler = new WindowsCancelHandler (DoWindowsConsoleCancelEvent);
#endif

#if !SSHARP
			[DllImport ("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
			private static extern int GetConsoleCP ();

			[DllImport ("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
			private static extern int GetConsoleOutputCP ();

			[DllImport ("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
			private static extern bool SetConsoleCtrlHandler (WindowsCancelHandler handler, bool addHandler);
#endif

#if CONSOLEIN
			// Only call the event handler if Control-C was pressed (code == 0), nothing else
			private static bool DoWindowsConsoleCancelEvent (int keyCode)
				{
				if (keyCode == 0)
					DoConsoleCancelEvent ();
				return keyCode == 0;
				}

			[MethodImpl (MethodImplOptions.NoInlining)]
			public static int GetInputCodePage ()
				{
				return GetConsoleCP ();
				}
#endif

			[MethodImpl (MethodImplOptions.NoInlining)]
			public static int GetOutputCodePage ()
				{
				return Encoding.Default.CodePage;
				}

#if CONSOLEIN
			public static void AddCtrlHandler ()
				{
				SetConsoleCtrlHandler (cancelHandler, true);
				ctrlHandlerAdded = true;
				}

			public static void RemoveCtrlHandler ()
				{
				SetConsoleCtrlHandler (cancelHandler, false);
				ctrlHandlerAdded = false;
				}
#endif
			}
#endif

		internal static TextWriter stdout;
		private static TextWriter stderr;
#if CONSOLEIN
		private static TextReader stdin;
#endif

#if SSHARP
		private static TextWriter _stdout;
		private static TextWriter _stderr;
#endif

		static Console ()
			{
#if NET_2_1 || SSHARP
#if CONSOLEIN
			Encoding inputEncoding;
#endif
			Encoding outputEncoding;
#endif

#if !SSHARP
			if (Environment.IsRunningOnWindows)
#endif
				{
				//
				// On Windows, follow the Windows tradition
				//
#if NET_2_1
	// should never happen since Moonlight does not run on windows
				inputEncoding = outputEncoding = Encoding.Default;
#else
				try
					{
#if CONSOLEIN
					inputEncoding = Encoding.GetEncoding (WindowsConsole.GetInputCodePage ());
#endif
					outputEncoding = Encoding.GetEncoding (WindowsConsole.GetOutputCodePage ());
					// ArgumentException and NotSupportedException can be thrown as well
					}
				catch
					{
#if CONSOLEIN
					// FIXME: I18N assemblies are not available when compiling mcs
					// Use Latin 1 as it is fast and UTF-8 is never used as console code page
					inputEncoding = 
#endif
					outputEncoding = Encoding.Default;
					}
#endif
				}
#if !SSHARP
			else
				{
				//
				// On Unix systems (128), do not output the
				// UTF-8 ZWNBSP (zero-width non-breaking space).
				//
				int code_page = 0;
				Encoding.InternalCodePage (ref code_page);

				if (code_page != -1 && ((code_page & 0x0fffffff) == 3 // UTF8Encoding.UTF8_CODE_PAGE
												|| ((code_page & 0x10000000) != 0)))
					inputEncoding = outputEncoding = Encoding.UTF8Unmarked;
				else
					inputEncoding = outputEncoding = Encoding.Default;
				}
#endif
#if CONSOLEIN
			SetupStreams (inputEncoding, outputEncoding);
#else
			SetupStreams (null, outputEncoding);
#endif
#if SSHARP
			_stdout = stdout;
			_stderr = stderr;
#endif
			}

		private static void SetupStreams (Encoding inputEncoding, Encoding outputEncoding)
			{
#if !NET_2_1 && !SSHARP
			if (!Environment.IsRunningOnWindows && ConsoleDriver.IsConsole)
				{
				StreamWriter w = new CStreamWriter (OpenStandardOutput (0), outputEncoding);
				w.AutoFlush = true;
				stdout = TextWriter.Synchronized (w, true);

				w = new CStreamWriter (OpenStandardOutput (0), outputEncoding);
				w.AutoFlush = true;
				stderr = TextWriter.Synchronized (w, true);

#if CONSOLEIN
				stdin = new CStreamReader (OpenStandardInput (0), inputEncoding);
#endif
				}
			else
				{
#endif
// FULL_AOT_RUNTIME is used (instead of MONOTOUCH) since we only want this code when running on 
// iOS (simulator or devices) and *not* when running tools (e.g. btouch #12179) that needs to use 
// the mscorlib.dll shipped with Xamarin.iOS
#if MONOTOUCH && FULL_AOT_RUNTIME
				stdout = new NSLogWriter ();
#else
#if SSHARP
				stdout = new StdTextWriter ();
#else
				stdout = new UnexceptionalStreamWriter (OpenStandardOutput (0), outputEncoding);
				((StreamWriter)stdout).AutoFlush = true;
#endif
#endif
				stdout = TextWriter.Synchronized (stdout, true);

#if MONOTOUCH && FULL_AOT_RUNTIME
				stderr = new NSLogWriter ();
#else
#if SSHARP
				stderr = new StdErrWriter ();
#else
				stderr = new UnexceptionalStreamWriter (OpenStandardError (0), outputEncoding);
				((StreamWriter)stderr).AutoFlush = true;
#endif
#endif
				stderr = TextWriter.Synchronized (stderr, true);

#if CONSOLEIN
				stdin = new UnexceptionalStreamReader (OpenStandardInput (0), inputEncoding);
				stdin = TextReader.Synchronized (stdin);
#endif
#if !NET_2_1 && !SSHARP
				}
#endif

#if MONODROID
			if (LogcatTextWriter.IsRunningOnAndroid ()) {
				stdout = TextWriter.Synchronized (new LogcatTextWriter ("mono-stdout", stdout));
				stderr = TextWriter.Synchronized (new LogcatTextWriter ("mono-stderr", stderr));
			}
#endif // MONODROID

			GC.SuppressFinalize (stdout);
			GC.SuppressFinalize (stderr);
#if CONSOLEIN
			GC.SuppressFinalize (stdin);
#endif

#if SSHARP
			IsOutputRedirected = false;
			IsErrorRedirected = false;
#endif
			}

		public static TextWriter Error
			{
			get { return stderr; }
			}

		public static TextWriter Out
			{
			get { return stdout; }
			}

#if SSHARP
		public static bool IsOutputRedirected
			{
			get;
			private set;
			}

		public static bool IsErrorRedirected
			{
			get;
			private set;
			}
#endif

#if CONSOLEIN
		public static TextReader In
			{
			get { return stdin; }
			}
#endif

#if !SSHARP
		private static Stream Open (IntPtr handle, FileAccess access, int bufferSize)
			{
			try
				{
				return new FileStream (handle, access, false, bufferSize, false, bufferSize == 0);
				}
			catch (IOException)
				{
				return Stream.Null;
				}
			}
#endif

		public static Stream OpenStandardError ()
			{
			return OpenStandardError (0);
			}

		// calling any FileStream constructor with a handle normally
		// requires permissions UnmanagedCode permissions. In this 
		// case we assert this permission so the console can be used
		// in partial trust (i.e. without having UnmanagedCode).
		[SecurityPermission (SecurityAction.Assert, UnmanagedCode = true)]
		public static Stream OpenStandardError (int bufferSize)
			{
#if SSHARP
			return new CrestronErrorLogStream ();
#else
			return Open (MonoIO.ConsoleError, FileAccess.Write, bufferSize);
#endif
			}

#if CONSOLEIN
		public static Stream OpenStandardInput ()
			{
			return OpenStandardInput (0);
			}

		// calling any FileStream constructor with a handle normally
		// requires permissions UnmanagedCode permissions. In this 
		// case we assert this permission so the console can be used
		// in partial trust (i.e. without having UnmanagedCode).
		[SecurityPermission (SecurityAction.Assert, UnmanagedCode = true)]
		public static Stream OpenStandardInput (int bufferSize)
			{
			return Open (MonoIO.ConsoleInput, FileAccess.Read, bufferSize);
			}
#endif

		public static Stream OpenStandardOutput ()
			{
			return OpenStandardOutput (0);
			}

		// calling any FileStream constructor with a handle normally
		// requires permissions UnmanagedCode permissions. In this 
		// case we assert this permission so the console can be used
		// in partial trust (i.e. without having UnmanagedCode).
		[SecurityPermission (SecurityAction.Assert, UnmanagedCode = true)]
		public static Stream OpenStandardOutput (int bufferSize)
			{
#if SSHARP
			return new CrestronConsoleStream ();
#else
			return Open (MonoIO.ConsoleOutput, FileAccess.Write, bufferSize);
#endif
			}

		[SecurityPermission (SecurityAction.Demand, UnmanagedCode = true)]
		public static void SetError (TextWriter newError)
			{
			if (newError == null)
				throw new ArgumentNullException ("newError");

			stderr = newError;
			IsErrorRedirected = newError != _stderr;
			}

#if SSHARP
		public static void ResetError ()
			{
			stderr = _stderr;
			IsErrorRedirected = false;
			}
#endif

#if CONSOLEIN
		[SecurityPermission (SecurityAction.Demand, UnmanagedCode = true)]
		public static void SetIn (TextReader newIn)
			{
			if (newIn == null)
				throw new ArgumentNullException ("newIn");

			stdin = newIn;
			}
#endif

		[SecurityPermission (SecurityAction.Demand, UnmanagedCode = true)]
		public static void SetOut (TextWriter newOut)
			{
			if (newOut == null)
				throw new ArgumentNullException ("newOut");

			stdout = newOut;
			IsOutputRedirected = newOut != _stdout;
			}

#if SSHARP
		public static void ResetOut ()
			{
			stdout = _stdout;
			IsOutputRedirected = false;
			}
#endif

#if SSHARP
		public static void Write<T> (T value) where T:struct
			{
			WriteColored (value);
			}

		public static void Write (object value)
			{
			WriteColored (value);
			}

		public static void Write (string value)
			{
			WriteColored (value);
			}

		public static void Write (char[] buffer)
			{
			WriteColored (new string (buffer));
			}

		public static void Write (string format, object arg0)
			{
			WriteColored (format, arg0);
			}

		public static void Write (string format, params object[] arg)
			{
			if (arg == null)
				WriteColored (format);
			else
				WriteColored (format, arg);
			}

		public static void Write (char[] buffer, int index, int count)
			{
			WriteColored (new string (buffer, index, count));
			}

		public static void Write (string format, object arg0, object arg1)
			{
			WriteColored (format, arg0, arg1);
			}

		public static void Write (string format, object arg0, object arg1, object arg2)
			{
			WriteColored (format, arg0, arg1, arg2);
			}

		[CLSCompliant (false)]
#if NETCF
		public static void Write (string format, object arg0, object arg1, object arg2, object arg3, params object[] arglist)
			{
			int argCount = arglist.Length;
#else
		public static void Write (string format, object arg0, object arg1, object arg2, object arg3, __arglist)
			{
			ArgIterator iter = new ArgIterator (__arglist);
			int argCount = iter.GetRemainingCount ();
#endif

			object[] args = new object[argCount + 4];
			args[0] = arg0;
			args[1] = arg1;
			args[2] = arg2;
			args[3] = arg3;
#if NETCF
			for (int i = 0; i < argCount; i++)
				{
				args[i + 4] = arglist[i];
#else
			for (int i = 0; i < argCount; i++)
				{
				TypedReference typedRef = iter.GetNextArg ();
				args[i + 4] = TypedReference.ToObject (typedRef);
#endif
				}

			WriteColored (String.Format (format, args));
			}
#else
		public static void Write (bool value)
			{
			stdout.Write (value);
			}

		public static void Write (char value)
			{
			stdout.Write (value);
			}

		public static void Write (decimal value)
			{
			stdout.Write (value);
			}

		public static void Write (double value)
			{
			stdout.Write (value);
			}

		public static void Write (int value)
			{
			stdout.Write (value);
			}

		public static void Write (long value)
			{
			stdout.Write (value);
			}

		public static void Write (float value)
			{
			stdout.Write (value);
			}

		[CLSCompliant (false)]
		public static void Write (uint value)
			{
			stdout.Write (value);
			}

		[CLSCompliant (false)]
		public static void Write (ulong value)
			{
			stdout.Write (value);
			}

		public static void Write (object value)
			{
			stdout.Write (value);
			}

		public static void Write (string value)
			{
			stdout.Write (value);
			}

		public static void Write (char[] buffer)
			{
			stdout.Write (buffer);
			}

		public static void Write (string format, object arg0)
			{
			stdout.Write (format, arg0);
			}

		public static void Write (string format, params object[] arg)
			{
			if (arg == null)
				stdout.Write (format);
			else
				stdout.Write (format, arg);
			}

		public static void Write (char[] buffer, int index, int count)
			{
			stdout.Write (buffer, index, count);
			}

		public static void Write (string format, object arg0, object arg1)
			{
			stdout.Write (format, arg0, arg1);
			}

		public static void Write (string format, object arg0, object arg1, object arg2)
			{
			stdout.Write (format, arg0, arg1, arg2);
			}

		[CLSCompliant (false)]
#if NETCF
		public static void Write (string format, object arg0, object arg1, object arg2, object arg3, params object[] arglist)
			{
			int argCount = arglist.Length;
#else
		public static void Write (string format, object arg0, object arg1, object arg2, object arg3, __arglist)
			{
			ArgIterator iter = new ArgIterator (__arglist);
			int argCount = iter.GetRemainingCount ();
#endif

			object[] args = new object[argCount + 4];
			args[0] = arg0;
			args[1] = arg1;
			args[2] = arg2;
			args[3] = arg3;
#if NETCF
			for (int i = 0; i < argCount; i++)
				{
				args[i + 4] = arglist[i];
#else
			for (int i = 0; i < argCount; i++)
				{
				TypedReference typedRef = iter.GetNextArg ();
				args[i + 4] = TypedReference.ToObject (typedRef);
#endif
				}

			stdout.Write (String.Format (format, args));
			}
#endif

		public static void WriteLine ()
			{
			stdout.WriteLine ();
			}

#if SSHARP
		public static void WriteLine<T> (T value) where T:struct
			{
			WriteColoredLine (value);
			}

		public static void WriteLine (object value)
			{
			WriteColoredLine (value);
			}

		public static void WriteLine (string value)
			{
			WriteColoredLine (value);
			}

		public static void WriteLine (char[] buffer)
			{
			WriteColoredLine (new string (buffer));
			}

		public static void WriteLine (string format, object arg0)
			{
			WriteColoredLine (format, arg0);
			}

		public static void WriteLine (string format, params object[] arg)
			{
			if (arg == null)
				WriteColoredLine (format);
			else
				WriteColoredLine (format, arg);
			}

		public static void WriteLine (char[] buffer, int index, int count)
			{
			WriteColoredLine (new string (buffer, index, count));
			}

		public static void WriteLine (string format, object arg0, object arg1)
			{
			WriteColoredLine (format, arg0, arg1);
			}

		public static void WriteLine (string format, object arg0, object arg1, object arg2)
			{
			WriteColoredLine (format, arg0, arg1, arg2);
			}

		[CLSCompliant (false)]
#if NETCF
		public static void WriteLine (string format, object arg0, object arg1, object arg2, object arg3, params object[] arglist)
			{
			int argCount = arglist.Length;
#else
		public static void WriteLine (string format, object arg0, object arg1, object arg2, object arg3, __arglist)
			{
			ArgIterator iter = new ArgIterator (__arglist);
			int argCount = iter.GetRemainingCount ();
#endif
			object[] args = new object[argCount + 4];
			args[0] = arg0;
			args[1] = arg1;
			args[2] = arg2;
			args[3] = arg3;
			for (int i = 0; i < argCount; i++)
				{
#if NETCF
				args[i + 4] = arglist[i];
#else
				TypedReference typedRef = iter.GetNextArg ();
				args[i + 4] = TypedReference.ToObject (typedRef);
#endif
				}

			WriteColoredLine (String.Format (format, args));
			}
#else
		public static void WriteLine (bool value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (char value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (decimal value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (double value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (int value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (long value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (float value)
			{
			stdout.WriteLine (value);
			}

		[CLSCompliant (false)]
		public static void WriteLine (uint value)
			{
			stdout.WriteLine (value);
			}

		[CLSCompliant (false)]
		public static void WriteLine (ulong value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (object value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (string value)
			{
			stdout.WriteLine (value);
			}

		public static void WriteLine (char[] buffer)
			{
			stdout.WriteLine (buffer);
			}

		public static void WriteLine (string format, object arg0)
			{
			stdout.WriteLine (format, arg0);
			}

		public static void WriteLine (string format, params object[] arg)
			{
			if (arg == null)
				stdout.WriteLine (format);
			else
				stdout.WriteLine (format, arg);
			}

		public static void WriteLine (char[] buffer, int index, int count)
			{
			stdout.WriteLine (buffer, index, count);
			}

		public static void WriteLine (string format, object arg0, object arg1)
			{
			stdout.WriteLine (format, arg0, arg1);
			}

		public static void WriteLine (string format, object arg0, object arg1, object arg2)
			{
			stdout.WriteLine (format, arg0, arg1, arg2);
			}

		[CLSCompliant (false)]
#if NETCF
		public static void WriteLine (string format, object arg0, object arg1, object arg2, object arg3, params object[] arglist)
			{
			int argCount = arglist.Length;
#else
		public static void WriteLine (string format, object arg0, object arg1, object arg2, object arg3, __arglist)
			{
			ArgIterator iter = new ArgIterator (__arglist);
			int argCount = iter.GetRemainingCount ();
#endif
			object[] args = new object[argCount + 4];
			args[0] = arg0;
			args[1] = arg1;
			args[2] = arg2;
			args[3] = arg3;
			for (int i = 0; i < argCount; i++)
				{
#if NETCF
				args[i + 4] = arglist[i];
#else
				TypedReference typedRef = iter.GetNextArg ();
				args[i + 4] = TypedReference.ToObject (typedRef);
#endif
				}

			stdout.WriteLine (String.Format (format, args));
			}
#endif

#if CONSOLEIN
#if !NET_2_1 && !SSHARP
		public static int Read ()
			{
			if ((stdin is CStreamReader) && ConsoleDriver.IsConsole)
				return ConsoleDriver.Read ();
			else
				return stdin.Read ();
			}

		public static string ReadLine ()
			{
			if ((stdin is CStreamReader) && ConsoleDriver.IsConsole)
				return ConsoleDriver.ReadLine ();
			else
				return stdin.ReadLine ();
			}
#else
		public static int Read ()
		{
			return stdin.Read ();
		}

		public static string ReadLine ()
		{
			return stdin.ReadLine ();
		}

#endif
#endif

#if !NET_2_1
		// FIXME: Console should use these encodings when changed
#if CONSOLEIN
		private static Encoding inputEncoding;
#endif
#if !SSHARP
		private static Encoding outputEncoding;
#endif

#if CONSOLEIN
		public static Encoding InputEncoding
			{
			get { return inputEncoding; }
			set
				{
				inputEncoding = value;
				SetupStreams (inputEncoding, outputEncoding);
				}
			}
#endif

		public static Encoding OutputEncoding
			{
#if SSHARP
			get { return Encoding.ASCII; }
#else
			get { return outputEncoding; }
			set
				{
				outputEncoding = value;
#if CONSOLEIN
				SetupStreams (inputEncoding, outputEncoding);
#else
				SetupStreams (null, outputEncoding);
#endif
				}
#endif
			}

#if SSHARP
		private static string _ansiiColorString;
		private const string _ansiiResetString = "\x1b[0m";

		private static ConsoleColor _backgroundColor = ConsoleColor.Black;
		public static ConsoleColor BackgroundColor
			{
			get { return _backgroundColor; }
			set
				{
				_backgroundColor = value;
				SetAnsiiString ();
				}
			}

		private static ConsoleColor _foregroundColor = ConsoleColor.White;
		public static ConsoleColor ForegroundColor
			{
			get { return _foregroundColor; }
			set
				{
				_foregroundColor = value;
				SetAnsiiString ();
				}
			}

		private static Dictionary<ConsoleColor, int> _dictColors = new Dictionary<ConsoleColor, int>
			{
				{ConsoleColor.Black, 30},
				{ConsoleColor.Red, 91},
				{ConsoleColor.Green, 92},
				{ConsoleColor.Yellow, 93},
				{ConsoleColor.Blue, 94},
				{ConsoleColor.Magenta, 95},
				{ConsoleColor.Cyan, 96},
				{ConsoleColor.White, 97},
				{ConsoleColor.DarkGray, 90},
				{ConsoleColor.DarkRed, 31},
				{ConsoleColor.DarkGreen, 32},
				{ConsoleColor.DarkYellow, 33},
				{ConsoleColor.DarkBlue, 34},
				{ConsoleColor.DarkMagenta, 35},
				{ConsoleColor.DarkCyan, 36},
				{ConsoleColor.Gray, 37}
			};

		private static void SetAnsiiString ()
			{
			if (_foregroundColor == ConsoleColor.White && _backgroundColor == ConsoleColor.Black)
				_ansiiColorString = null;
			else
				{
				var foreColor = _dictColors[_foregroundColor];
				var backColor = _dictColors[_backgroundColor];

				_ansiiColorString = String.Format ("\x1b[{0};{1}m", foreColor, backColor + 10);
				}
			}

		public static void ResetColor ()
			{
			_ansiiColorString = null;
			_foregroundColor = ConsoleColor.White;
			_backgroundColor = ConsoleColor.Black;
			}

		private static void WriteColoredLine<T> (T obj)
			{
			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.WriteLine (obj);
			else
				stdout.WriteLine ("{0}{1}{2}", _ansiiColorString, obj, _ansiiResetString);
			}

		private static void WriteColoredLine (string format, params object[] args)
			{
			if (args == null)
				args = new object[0];

			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.WriteLine (format, args);
			else
				stdout.WriteLine ("{0}{1}{2}", _ansiiColorString, String.Format (format, args), _ansiiResetString);
			}

		private static void WriteColoredLine (string message)
			{
			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.WriteLine (message);
			else
				stdout.WriteLine ("{0}{1}{2}", _ansiiColorString, message, _ansiiResetString);
			}

		private static void WriteColored<T> (T obj)
			{
			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.Write (obj);
			else
				stdout.Write ("{0}{1}{2}", _ansiiColorString, obj, _ansiiResetString);
			}

		private static void WriteColored (string format, params object[] args)
			{
			if (args == null)
				args = new object[0];

			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.Write (format, args);
			else
				stdout.Write ("{0}{1}{2}", _ansiiColorString, String.Format (format, args), _ansiiResetString);
			}

		private static void WriteColored (string message)
			{
			if (_ansiiColorString == null || IsOutputRedirected)
				stdout.Write (message);
			else
				stdout.Write ("{0}{1}{2}", _ansiiColorString, message, _ansiiResetString);
			}
#else
		public static ConsoleColor BackgroundColor
			{
			get { return ConsoleDriver.BackgroundColor; }
			set { ConsoleDriver.BackgroundColor = value; }
			}

		public static int BufferHeight
			{
			get { return ConsoleDriver.BufferHeight; }
			[MonoLimitation ("Implemented only on Windows")] set { ConsoleDriver.BufferHeight = value; }
			}

		public static int BufferWidth
			{
			get { return ConsoleDriver.BufferWidth; }
			[MonoLimitation ("Implemented only on Windows")] set { ConsoleDriver.BufferWidth = value; }
			}

		[MonoLimitation ("Implemented only on Windows")]
		public static bool CapsLock
			{
			get { return ConsoleDriver.CapsLock; }
			}

		public static int CursorLeft
			{
			get { return ConsoleDriver.CursorLeft; }
			set { ConsoleDriver.CursorLeft = value; }
			}

		public static int CursorTop
			{
			get { return ConsoleDriver.CursorTop; }
			set { ConsoleDriver.CursorTop = value; }
			}

		public static int CursorSize
			{
			get { return ConsoleDriver.CursorSize; }
			set { ConsoleDriver.CursorSize = value; }
			}

		public static bool CursorVisible
			{
			get { return ConsoleDriver.CursorVisible; }
			set { ConsoleDriver.CursorVisible = value; }
			}

		public static ConsoleColor ForegroundColor
			{
			get { return ConsoleDriver.ForegroundColor; }
			set { ConsoleDriver.ForegroundColor = value; }
			}

		public static bool KeyAvailable
			{
			get { return ConsoleDriver.KeyAvailable; }
			}

		public static int LargestWindowHeight
			{
			get { return ConsoleDriver.LargestWindowHeight; }
			}

		public static int LargestWindowWidth
			{
			get { return ConsoleDriver.LargestWindowWidth; }
			}

		[MonoLimitation ("Only works on windows")]
		public static bool NumberLock
			{
			get { return ConsoleDriver.NumberLock; }
			}

		public static string Title
			{
			get { return ConsoleDriver.Title; }
			set { ConsoleDriver.Title = value; }
			}

		public static bool TreatControlCAsInput
			{
			get { return ConsoleDriver.TreatControlCAsInput; }
			set { ConsoleDriver.TreatControlCAsInput = value; }
			}

		[MonoLimitation ("Only works on windows")]
		public static int WindowHeight
			{
			get { return ConsoleDriver.WindowHeight; }
			set { ConsoleDriver.WindowHeight = value; }
			}

		[MonoLimitation ("Only works on windows")]
		public static int WindowLeft
			{
			get { return ConsoleDriver.WindowLeft; }
			set { ConsoleDriver.WindowLeft = value; }
			}

		[MonoLimitation ("Only works on windows")]
		public static int WindowTop
			{
			get { return ConsoleDriver.WindowTop; }
			set { ConsoleDriver.WindowTop = value; }
			}

		[MonoLimitation ("Only works on windows")]
		public static int WindowWidth
			{
			get { return ConsoleDriver.WindowWidth; }
			set { ConsoleDriver.WindowWidth = value; }
			}

#if NET_4_5
		public static bool IsErrorRedirected {
			get {
				return ConsoleDriver.IsErrorRedirected;
			}
		}

		public static bool IsOutputRedirected {
			get {
				return ConsoleDriver.IsOutputRedirected;
			}
		}

		public static bool IsInputRedirected {
			get {
				return ConsoleDriver.IsInputRedirected;
			}
		}
#endif

		public static void Beep ()
			{
			Beep (1000, 500);
			}

		public static void Beep (int frequency, int duration)
			{
			if (frequency < 37 || frequency > 32767)
				throw new ArgumentOutOfRangeException ("frequency");

			if (duration <= 0)
				throw new ArgumentOutOfRangeException ("duration");

			ConsoleDriver.Beep (frequency, duration);
			}

		public static void Clear ()
			{
			ConsoleDriver.Clear ();
			}

		[MonoLimitation ("Implemented only on Windows")]
		public static void MoveBufferArea (int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int targetLeft, int targetTop)
			{
			ConsoleDriver.MoveBufferArea (sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop);
			}

		[MonoLimitation ("Implemented only on Windows")]
		public static void MoveBufferArea (
			int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int targetLeft, int targetTop, Char sourceChar, ConsoleColor sourceForeColor,
			ConsoleColor sourceBackColor)
			{
			ConsoleDriver.MoveBufferArea (sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop, sourceChar, sourceForeColor, sourceBackColor);
			}

		public static ConsoleKeyInfo ReadKey ()
			{
			return ReadKey (false);
			}

		public static ConsoleKeyInfo ReadKey (bool intercept)
			{
			return ConsoleDriver.ReadKey (intercept);
			}

		public static void ResetColor ()
			{
			ConsoleDriver.ResetColor ();
			}

		[MonoLimitation ("Only works on windows")]
		public static void SetBufferSize (int width, int height)
			{
			ConsoleDriver.SetBufferSize (width, height);
			}

		public static void SetCursorPosition (int left, int top)
			{
			ConsoleDriver.SetCursorPosition (left, top);
			}

		public static void SetWindowPosition (int left, int top)
			{
			ConsoleDriver.SetWindowPosition (left, top);
			}

		public static void SetWindowSize (int width, int height)
			{
			ConsoleDriver.SetWindowSize (width, height);
			}

		private static ConsoleCancelEventHandler cancel_event;

		public static event ConsoleCancelEventHandler CancelKeyPress
			{
			add
				{
				if (ConsoleDriver.Initialized == false)
					ConsoleDriver.Init ();

				cancel_event += value;

				if (Environment.IsRunningOnWindows && !WindowsConsole.ctrlHandlerAdded)
					WindowsConsole.AddCtrlHandler ();
				}
			remove
				{
				if (ConsoleDriver.Initialized == false)
					ConsoleDriver.Init ();

				cancel_event -= value;

				if (cancel_event == null && Environment.IsRunningOnWindows)
					{
					// Need to remove our hook if there's nothing left in the event
					if (WindowsConsole.ctrlHandlerAdded)
						WindowsConsole.RemoveCtrlHandler ();
					}
				}
			}

		private delegate void InternalCancelHandler ();

#pragma warning disable 414
		//
		// Used by console-io.c
		//
		private static readonly InternalCancelHandler cancel_handler = new InternalCancelHandler (DoConsoleCancelEvent);
#pragma warning restore 414		

		internal static void DoConsoleCancelEvent ()
			{
			bool exit = true;
			if (cancel_event != null)
				{
				ConsoleCancelEventArgs args = new ConsoleCancelEventArgs (ConsoleSpecialKey.ControlC);
				Delegate[] delegates = cancel_event.GetInvocationList ();
				foreach (ConsoleCancelEventHandler d in delegates)
					{
					try
						{
						// Sender is always null here.
						d (null, args);
						}
					catch
						{
						} // Ignore any exception.
					}
				exit = !args.Cancel;
				}

			if (exit)
				Environment.Exit (58);
			}
#endif

#if SSHARP
		private sealed class StdTextWriter : TextWriter
			{
			public StdTextWriter ()
				: base (null)
				{
				}

			public override void Write (char value)
				{
				string str = new string (value, 1);
				this.Write (str);
				}

			public override void Write (string value)
				{
				if (value != null)
					{
					CrestronConsole.Print (value);
					}
				}

			public override void Write (char[] buffer, int index, int count)
				{
				if (buffer == null)
					{
					throw new ArgumentNullException ("buffer");
					}
				if (index < 0)
					{
					throw new ArgumentOutOfRangeException ("index");
					}
				if (count < 0)
					{
					throw new ArgumentOutOfRangeException ("count");
					}
				if ((buffer.Length - index) < count)
					{
					throw new ArgumentException (null, "count");
					}

				CrestronConsole.Print (new string (buffer, index, count));
				}

			public override Encoding Encoding
				{
				get
					{
					return Encoding.ASCII;
					}
				}
			}
		private sealed class StdErrWriter : TextWriter
			{
			public StdErrWriter ()
				: base (null)
				{
				}

			public override void Write (char value)
				{
				string str = new string (value, 1);
				this.Write (str);
				}

			public override void Write (string value)
				{
				if (value != null)
					{
					ErrorLog.Error (value);
					}
				}

			public override void Write (char[] buffer, int index, int count)
				{
				if (buffer == null)
					{
					throw new ArgumentNullException ("buffer");
					}
				if (index < 0)
					{
					throw new ArgumentOutOfRangeException ("index");
					}
				if (count < 0)
					{
					throw new ArgumentOutOfRangeException ("count");
					}
				if ((buffer.Length - index) < count)
					{
					throw new ArgumentException (null, "count");
					}

				ErrorLog.Error (new string (buffer, index, count));
				}

			public override Encoding Encoding
				{
				get
					{
					return Encoding.ASCII;
					}
				}
			}
#endif
#endif
		}
	}
