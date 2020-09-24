#region Copyright (c) 2000-2020 Developer Express Inc.
/*
{*******************************************************************}
{                                                                   }
{       Developer Express .NET Component Library                    }
{                                                                   }
{                                                                   }
{       Copyright (c) 2000-2020 Developer Express Inc.              }
{       ALL RIGHTS RESERVED                                         }
{                                                                   }
{   The entire contents of this file is protected by U.S. and       }
{   International Copyright Laws. Unauthorized reproduction,        }
{   reverse-engineering, and distribution of all or any portion of  }
{   the code contained in this file is strictly prohibited and may  }
{   result in severe civil and criminal penalties and will be       }
{   prosecuted to the maximum extent possible under the law.        }
{                                                                   }
{   RESTRICTIONS                                                    }
{                                                                   }
{   THIS SOURCE CODE AND ALL RESULTING INTERMEDIATE FILES           }
{   ARE CONFIDENTIAL AND PROPRIETARY TRADE                          }
{   SECRETS OF DEVELOPER EXPRESS INC. THE REGISTERED DEVELOPER IS   }
{   LICENSED TO DISTRIBUTE THE PRODUCT AND ALL ACCOMPANYING .NET    }
{   CONTROLS AS PART OF AN EXECUTABLE PROGRAM ONLY.                 }
{                                                                   }
{   THE SOURCE CODE CONTAINED WITHIN THIS FILE AND ALL RELATED      }
{   FILES OR ANY PORTION OF ITS CONTENTS SHALL AT NO TIME BE        }
{   COPIED, TRANSFERRED, SOLD, DISTRIBUTED, OR OTHERWISE MADE       }
{   AVAILABLE TO OTHER INDIVIDUALS WITHOUT EXPRESS WRITTEN CONSENT  }
{   AND PERMISSION FROM DEVELOPER EXPRESS INC.                      }
{                                                                   }
{   CONSULT THE END USER LICENSE AGREEMENT FOR INFORMATION ON       }
{   ADDITIONAL RESTRICTIONS.                                        }
{                                                                   }
{*******************************************************************}
*/
#endregion Copyright (c) 2000-2020 Developer Express Inc.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Text;
using DevExpress.Utils;
using Microsoft.Win32;
namespace DevExpress.Persistent.Base {
	public enum FileLocation { None, ApplicationFolder, CurrentUserApplicationDataFolder }
	public class NeedContextInformationEventArgs : EventArgs {
		private string contextInformation = string.Empty;
		public string ContextInformation {
			get { return contextInformation; }
			set { contextInformation = value; }
		}
	}
	public class CustomFormatDateTimeStampEventArgs : EventArgs {
		public CustomFormatDateTimeStampEventArgs(DateTime dateTime, string result) {
			this.DateTime = dateTime;
			this.Result = result;
		}
		public DateTime DateTime { get; private set; }
		public string Result { get; set; }
	}
	public delegate void Method();
	[Serializable]
	public class DelayedException : Exception {
#if DXCORE3 || NETSTANDARD
		private readonly string targetObjectIdentifier;
		protected DelayedException(SerializationInfo info, StreamingContext context)
			: base(info, context) {
			this.targetObjectIdentifier = info.GetString(nameof(TargetObjectIdentifier));
		}
		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			Guard.ArgumentNotNull(info, nameof(info));
			info.AddValue(nameof(TargetObjectIdentifier), targetObjectIdentifier);
			base.GetObjectData(info, context);
		}
#else
		[Serializable]
		private struct DelayedExceptionState : ISafeSerializationData {
			public string TargetObjectIdentifier { get; set; }
			void ISafeSerializationData.CompleteDeserialization(Object obj) {
				DelayedException exception = (DelayedException)obj;
				exception.state = this;
			}
		}
		[NonSerialized]
		private DelayedExceptionState state = new DelayedExceptionState();
#endif
		private readonly object targetObject;
		public DelayedException(Exception exception, object targetObject, string targetObjectIdentifier)
			: base(FormatMessage(exception.Message, targetObject, targetObjectIdentifier), exception) {
			this.targetObject = targetObject;
#if DXCORE3 || NETSTANDARD
			this.targetObjectIdentifier = targetObjectIdentifier;
#else
			state.TargetObjectIdentifier = targetObjectIdentifier;
			SerializeObjectState += (e, eventArgs) => eventArgs.AddSerializedState(state);
#endif
		}
		public static string FormatMessage(string errorMessage, object targetObject, string targetObjectIdentifier) {
			string additionalMessage = targetObject != null ? "'" + targetObject.GetType() + "'" : "";
			if(!string.IsNullOrEmpty(targetObjectIdentifier)) {
				if(!string.IsNullOrEmpty(additionalMessage)) {
					additionalMessage += ", ";
				}
				additionalMessage += "'" + targetObjectIdentifier + "'";
			}
			return !string.IsNullOrEmpty(additionalMessage) ? errorMessage + ". " + additionalMessage : errorMessage;
		}
		public object TargetObject {
			get { return targetObject; }
		}
		public string TargetObjectIdentifier {
			get {
#if DXCORE3 || NETSTANDARD
				return targetObjectIdentifier;
#else
				return state.TargetObjectIdentifier;
#endif
			}
		}
	}
	public class SafeExecutor {
		private List<DelayedException> exceptionEntries = new List<DelayedException>();
		private object targetObject;
		private string targetObjectIdentifier;
		public SafeExecutor(object targetObject) : this(targetObject, "") { }
		public SafeExecutor(object targetObject, string targetObjectIdentifier) {
			this.targetObject = targetObject;
			this.targetObjectIdentifier = targetObjectIdentifier;
		}
		public static bool TryExecute(Method method) {
			try {
				method();
				return true;
			}
			catch {
				return false;
			}
		}
		public void Execute(Method method, object targetObject, string targetObjectIdentifier) {
			try {
				method();
			}
			catch(Exception e) {
				if(Debugger.IsAttached) {
					throw;
				}
				else {
					exceptionEntries.Add(new DelayedException(e, targetObject, targetObjectIdentifier));
				}
			}
		}
		public void Execute(Method method) {
			Execute(method, null, null);
		}
		public void Dispose(IDisposable targetObject) {
			Dispose(targetObject, "");
		}
		public void Dispose(IDisposable targetObject, string targetObjectIdentifier) {
			Execute(() => targetObject.Dispose(), targetObject, targetObjectIdentifier);
		}
		public List<DelayedException> Exceptions {
			get { return exceptionEntries; }
		}
		public void ThrowExceptionIfAny() {
			if(exceptionEntries.Count > 0) {
				throw new DelayedExceptionList(exceptionEntries, targetObject, targetObjectIdentifier);
			}
		}
	}
	[Serializable]
	public class DelayedExceptionList : Exception {
#if DXCORE3 || NETSTANDARD
		private readonly List<DelayedException> exceptions;
		protected DelayedExceptionList(SerializationInfo info, StreamingContext context)
			: base(info, context) {
			this.exceptions = info.GetValue(nameof(Exceptions), typeof(List<DelayedException>)) as List<DelayedException>;
		}
		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			Guard.ArgumentNotNull(info, nameof(info));
			info.AddValue(nameof(Exceptions), exceptions, typeof(List<DelayedException>));
			base.GetObjectData(info, context);
		}
#else
		[Serializable]
		private struct DelayedExceptionListState : ISafeSerializationData {
			public List<DelayedException> Exceptions { get; set; }
			void ISafeSerializationData.CompleteDeserialization(Object obj) {
				DelayedExceptionList exception = (DelayedExceptionList)obj;
				exception.state = this;
			}
		}
		[NonSerialized]
		private DelayedExceptionListState state = new DelayedExceptionListState();
#endif
		public static string FormatMessage(string errorMessage, object targetObject, string targetObjectId) {
			return DelayedException.FormatMessage(errorMessage, targetObject, targetObjectId);
		}
		public DelayedExceptionList(List<DelayedException> exceptions, object targetObject, string targetObjectId)
			: base(FormatMessage(exceptions[0].Message, targetObject, targetObjectId), exceptions[0]) {
#if DXCORE3 || NETSTANDARD
			this.exceptions = exceptions;
#else
			state.Exceptions = exceptions;
			SerializeObjectState += (exception, eventArgs) => eventArgs.AddSerializedState(state);
#endif
		}
		public List<DelayedException> Exceptions {
			get {
#if DXCORE3 || NETSTANDARD
				return exceptions;
#else
				return state.Exceptions;
#endif
			}
		}
	}
	public static class PathHelper {
		public static string GetApplicationFolder() {
#if !NETSTANDARD && !NETCOREAPP
			return AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
#else
			return AppDomain.CurrentDomain.BaseDirectory;
#endif
		}
	}
	public class CreateCustomTracerEventArgs : EventArgs {
		private String outputFile;
		public CreateCustomTracerEventArgs(String outputFile) {
			this.outputFile = outputFile;
		}
		public String OutputFile {
			get { return outputFile; }
		}
		public Tracing Tracer { get; set; }
	}
	public class Tracing : IDisposable {
		public const string TraceLogLocationKey = "TraceLogLocation";
		public const string TraceListenerName = "XAFTraceListener";
		public const string SwitchName = "eXpressAppFramework";
		public const string NetCoreSwitchName = "eXpressAppFrameworkTraceLevel";
		public const char FieldDelimiter = '\t';
		public readonly static string EmptyHeader = new string(FieldDelimiter, 7);
		public const string DateTimeFormat = "dd.MM.yy HH:mm:ss.fff";
		public static string LogName = SwitchName;
		public static string LocalUserAppDataPath = string.Empty;
		public static bool UseConfigurationManager = true;
		private const int lastEntriesMaxCountDefault = 100;
		private static object lockObject = new object();
		private static bool traceLockedSections = false;
		private static string outputDirectory;
		private static FileLocation? fileLocation;
		private static readonly object[] EmptyArgs = new object[0];
		private TraceSwitch verbositySwitch;
		private List<string> cache = new List<string>();
		private int lockCount;
		private int lastEntriesMaxCount = lastEntriesMaxCountDefault;
		private Queue<string> lastEntries = new Queue<string>(lastEntriesMaxCountDefault);
		private readonly object internalLockObject = new object();
		protected static string SectionDelim = new string('=', 80);
		protected static string SubSectionDelim = new string('-', 80);
		private static string OutputDirectory {
			get {
				if(outputDirectory == null) {
					lock(lockObject) {
						if(outputDirectory == null) {
							try {
								outputDirectory = PathHelper.GetApplicationFolder();
							}
							catch(SecurityException) {
								outputDirectory = "";
							}
						}
					}
				}
				return outputDirectory;
			}
		}
		private void FlushCache() {
			if(cache.Count > 0) {
				string message = string.Join("\r\n", cache);
				WriteLine(message, null);
				cache.Clear();
			}
		}
		protected virtual string GetDateTimeStamp() {
			DateTime dateTime = DateTime.Now;
			CustomFormatDateTimeStampEventArgs args = new CustomFormatDateTimeStampEventArgs(dateTime, dateTime.ToString(DateTimeFormat, DateTimeFormatInfo.InvariantInfo));
			if(CustomFormatDateTimeStamp != null) {
				CustomFormatDateTimeStamp(this, args);
			}
			return args.Result;
		}
		private void LogHeader() {
			bool condition = verbositySwitch.Level != TraceLevel.Off;
			WriteLineIf(condition, SectionDelim);
			WriteLineIf(condition, string.Format("Trace Log for {0} is started", AppDomain.CurrentDomain.FriendlyName));
			WriteLineIf(condition, SectionDelim);
		}
		private string EnumerateNetFrameworkVersions() {
			try {
				using(RegistryKey frameworkRegKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NETFramework", RegistryKeyPermissionCheck.Default, System.Security.AccessControl.RegistryRights.QueryValues)) {
					List<string> items = new List<string>();
					string frameworkRootDirectory = (string)frameworkRegKey.GetValue("InstallRoot", "");
					foreach(string frameworkDir in Directory.GetDirectories(frameworkRootDirectory)) {
						if(File.Exists(Path.Combine(frameworkDir, "fusion.dll"))) {
							items.Add(Path.GetFileName(frameworkDir));
						}
					}
					return string.Join(", ", items.ToArray());
				}
			} catch(Exception e) {
				return string.Format("Unable to enumerate .Net Framework versions. Error: {0}", e.Message);
			}
		}
		private void LogStartupInformation() {
			HandledEventArgs args = new HandledEventArgs();
			if(CustomLogStartupInformation != null) {
				CustomLogStartupInformation(this, args);
			}
			if(!args.Handled) {
				StringBuilder report = new StringBuilder();
				report.AppendLine("System Environment");
				report.Append("\tOS Version: ").AppendLine(Environment.OSVersion.VersionString);
				report.Append("\t.Net Framework Versions: ").AppendLine(EnumerateNetFrameworkVersions());
				report.Append("\tCLR Version: ").AppendLine(Environment.Version.ToString());
				report.Append("\teXpressApp Version: ").AppendLine(AssemblyInfo.Version);
				report.Append("\teXpressApp File Version: ").AppendLine(AssemblyInfo.FileVersion);
				report.AppendLine();
				report.AppendLine("Application config");
				if(UseConfigurationManager) {
					foreach(string key in ConfigurationManager.AppSettings.AllKeys) {
						report.Append("\t").Append(key).Append("=").AppendLine(ConfigurationManager.AppSettings[key]);
					}
				}
				LogText(report.ToString());
			}
		}
		private static string defaultLevel = "0";
#if NETSTANDARD || DXCORE3
		private string filename;
#endif
		protected Tracing(string filename) {
			listener = new TextWriterTraceListener(filename, TraceListenerName);
#if NETSTANDARD || DXCORE3
			this.filename = filename;
#endif
			try {
				InitializeVerbositySwitch("1");
			}
			catch(Exception e) {
				LogError(e);
				throw;
			}
		}
		protected Tracing() {
			InitializeCore();
		}
		private void InitializeVerbositySwitch(string defaultLevel) {
			string description = "0-Off, 1-Errors, 2-Warnings, 3-Info, 4-Verbose";
#if !NETSTANDARD && !NETCOREAPP
			verbositySwitch = new System.Diagnostics.TraceSwitch(SwitchName, description, defaultLevel);
#else
			if(verbositySwitch == null) {
				string switchValue = defaultLevel;
				if(UseConfigurationManager) {
					try {
						switchValue = ConfigurationManager.AppSettings.Get(NetCoreSwitchName);
					}
					catch(Exception) {
						verbositySwitch = new TraceSwitch(NetCoreSwitchName, description, "1");
						throw;
					}
					if(string.IsNullOrEmpty(switchValue)) {
						switchValue = defaultLevel;
					}
				}
				verbositySwitch = new TraceSwitch(NetCoreSwitchName, description, switchValue);
			}
#endif
		}
		private void InitializeCore() {
			if(AppDomain.CurrentDomain.FriendlyName.ToLower().Contains("domain-nunit.addin")) { 
				lock(lockObject) {
					for(int i = Trace.Listeners.Count - 1; i >= 0; i--) {
						if(Trace.Listeners[i] is DefaultTraceListener) {
							Trace.Listeners.RemoveAt(i);
						}
					}
				}
			}
			Exception initializeVerbositySwitchException = null;
			try {
				InitializeVerbositySwitch(defaultLevel);
			}
			catch(Exception e) {
				initializeVerbositySwitchException = e;
			}
#if !NETSTANDARD && !NETCOREAPP
			if(HasUnmanagedCodePermission) { 
#endif
				try {
					AddTraceLogListener();
					InitializeTraceAutoFlush();
				}
				catch(SecurityException) { }
#if !NETSTANDARD && !NETCOREAPP
			}
#endif
			if(initializeVerbositySwitchException != null) {
#if !NETSTANDARD && !NETCOREAPP
				LogError(initializeVerbositySwitchException);
#else
				if(initializeVerbositySwitchException is System.Configuration.ConfigurationErrorsException) {
					string documentationURL = "https://docs.devexpress.com/eXpressAppFramework/401264";
					string message = string.Format("Configuration system failed to initialize, refer to the {0} documentation article for a possible solution.", documentationURL);
					LogError(new Exception(message, initializeVerbositySwitchException));
				}
				else {
					LogError(initializeVerbositySwitchException);
				}
#endif
				throw initializeVerbositySwitchException;
			}
			LogHeader();
			LogStartupInformation();
		}
		private static void InitializeTraceAutoFlush() {
			Trace.AutoFlush = true;
		}
		private void AddTraceLogListener() {
			if(verbositySwitch.Level != TraceLevel.Off) {
				if(!string.IsNullOrEmpty(OutputDirectory)) {
					TextWriterTraceListener listener = new TextWriterTraceListener(Path.Combine(OutputDirectory, LogName + ".log"), TraceListenerName);
					lock(Trace.Listeners) {
						Trace.Listeners.Remove(TraceListenerName);
						Trace.Listeners.Add(listener);
					}
				}
			}
		}
		private static bool fileLocationWasReadWithError = false;
		public static FileLocation GetFileLocationFromSettings() {
			if(fileLocation == null && !fileLocationWasReadWithError) {
				fileLocation = FileLocation.ApplicationFolder;
				string value = null;
				if(UseConfigurationManager) {
					try {
						value = ConfigurationManager.AppSettings[TraceLogLocationKey];
					}
					catch(Exception e) {
						fileLocationWasReadWithError = true;
						Tracing.Tracer.LogError(e);
						throw;
					}
					if(!string.IsNullOrEmpty(value)) {
						fileLocation = (FileLocation)Enum.Parse(typeof(FileLocation), value, true);
					}
				}
			}
			return fileLocation.Value;
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public static string GetTraceLogDirectory() {
			GetFileLocationFromSettings();
			string outputDirectory = null;
			switch(fileLocation.Value) {
				case FileLocation.ApplicationFolder:
				outputDirectory = PathHelper.GetApplicationFolder();
				break;
				case FileLocation.CurrentUserApplicationDataFolder:
				outputDirectory = LocalUserAppDataPath;
				break;
				default:
				outputDirectory = "";
				break;
			}
			return outputDirectory;
		}
		public static bool TraceLockedSections {
			get { return traceLockedSections; }
			set { traceLockedSections = value; }
		}
		private static Tracing tracer;
		public static Tracing Tracer {
			get {
				if(tracer == null) {
					lock(lockObject) {
						if(tracer == null) {
							tracer = RaiseCreateCustomTracerEvent(null);
							if(tracer == null) {
								tracer = new Tracing();
							}
						}
					}
				}
				return tracer;
			}
		}
		public static Boolean IsTracerInitialized {
			get { return tracer != null; }
		}
		public TraceSwitch VerbositySwitch {
			get { return verbositySwitch; }
		}
		private static Dictionary<Guid, Tracing> tracingDictionary = new Dictionary<Guid, Tracing>();
		private TextWriterTraceListener listener;
		private static Tracing RaiseCreateCustomTracerEvent(String outputFile) {
			Tracing result = null;
			if(CreateCustomTracer != null) {
				CreateCustomTracerEventArgs customTracerEventArgs = new CreateCustomTracerEventArgs(outputFile);
				CreateCustomTracer(null, customTracerEventArgs);
				result = customTracerEventArgs.Tracer;
			}
			return result;
		}
		public static void Initialize(Guid key, string outputFile) {
			if(tracingDictionary.ContainsKey(key)) {
				tracingDictionary[key].Dispose();
				tracingDictionary.Remove(key);
			}
			Tracing tracer = RaiseCreateCustomTracerEvent(outputFile);
			if(tracer == null) {
				tracer = new Tracing(outputFile);
			}
			tracingDictionary.Add(key, tracer);
		}
		public static bool Initialize(string outputDirectory) {
			return Tracing.Initialize(outputDirectory, defaultLevel);
		}
		public static bool Initialize() {
			return Tracing.Initialize(GetTraceLogDirectory());
		}
		public static bool Initialize(string outputDirectory, string defaultLevel) {
			if(tracer == null) {
				lock(lockObject) {
					if(tracer == null) {
						Tracing.defaultLevel = defaultLevel;
						Tracing.outputDirectory = outputDirectory;
						tracer = RaiseCreateCustomTracerEvent(null);
						if(tracer == null) {
							tracer = new Tracing();
						}
						return true;
					}
				}
			}
			tracer.LogText("Initialization ignored.");
			return false;
		}
		public static bool Initialize(int defaultLevel) {
			return Tracing.Initialize(GetTraceLogDirectory(), defaultLevel.ToString());
		}
		public static void Close(bool deleteLog) {
			Tracing.Close();
			if(deleteLog) { 
				File.Delete(Path.Combine(Tracing.OutputDirectory, Tracing.LogName + ".log"));
			}
		}
		public static void Close() {
			if(tracer != null) {
				CreateCustomTracer = null;
				CustomLogStartupInformation = null;
				tracer.Dispose();
				tracer = null;
			}
		}
		public static void Close(Guid guid) {
			Tracing tracing;
			if(tracingDictionary.TryGetValue(guid, out tracing)) {
				tracingDictionary.Remove(guid);
				tracing.Dispose();
			}
		}
		private void WriteLineIfFormat(bool condition, string textFormat, object[] args, bool flushCache = false) {
			string text = args.Length == 0 ? textFormat : string.Format(textFormat, args); 
			WriteLineIf(condition, text, flushCache);
		}
		private void WriteLineIf(bool condition, string text, bool flushCache = false, Exception exception = null) {
			lock(internalLockObject) {
				string traceText = null;
				if(lastEntriesMaxCount > 0) {
					if(lastEntries.Count == lastEntriesMaxCount) {
						lastEntries.Dequeue();
					}
					traceText = CreateTraceText(text);
					string valueToCash = traceText.Length >= 150 ? traceText.Substring(0, 150) : traceText;
					lastEntries.Enqueue(valueToCash);
				}
				if(condition) {
					if(traceText == null) {
						traceText = CreateTraceText(text);
					}
					if(flushCache) {
						FlushCache();
						WriteLine(traceText, exception);
					}
					else if(lockCount == 0) {
						WriteLine(traceText, exception);
					}
					else {
						cache.Add(traceText);
					}
				}
			}
		}
		private string CreateTraceText(string text) {
			string traceText = GetDateTimeStamp() + FieldDelimiter.ToString();
			NeedContextInformationEventArgs args = new NeedContextInformationEventArgs();
			RaiseNeedContextInformation(args);
			if(!string.IsNullOrEmpty(args.ContextInformation)) {
				traceText += args.ContextInformation + FieldDelimiter.ToString();
			}
			return traceText + text;
		}
		private void WriteLine(string message, Exception exception) {
			if(listener != null) {
#if NETSTANDARD || DXCORE3
				if(listener.Writer == null) {
					Stream fileStream = new FileStream(filename, FileMode.Append);
					listener = new TextWriterTraceListener(fileStream, TraceListenerName);
				}
#endif
				listener.WriteLine(message);
				listener.Close();
			}
#if !NETSTANDARD && !NETCOREAPP
			else if(HasUnmanagedCodePermission) {
#else
			else {
#endif
				if(exception != null) {
					string resultText = message.Replace("{", "{{").Replace("}", "}}");
					Trace.TraceError(resultText, exception);
				}
				else {
					Trace.WriteLine(message);
				}
			}
		}
		private static string ArrayToString(Array array, HashSet<object> inProcess) {
			if(array.Length == 0) {
				return "<not specified>"; 
			}
			string result = "";
			foreach(object item in array) {
				result += "\r\n\t\t";
				if(item is Array) {
					if(item == array || inProcess.Contains(item)) {
						result += "<recursive reference>";
					}
					else {
						inProcess.Add(array);
						result += ArrayToString((Array)item, inProcess);
						inProcess.Remove(array);
					}
				}
				else {
					result += ObjectToString(item);
				}
			}
			return result;
		}
		private static string ObjectToString(object value) {
			return value == null ? "<not specified>" : value.ToString();
		}
		private void RaiseNeedContextInformation(NeedContextInformationEventArgs args) {
			if(NeedContextInformation != null) {
				NeedContextInformation(this, args);
			}
		}
		protected static void FormatExceptionReport(Exception exception, List<string> report, string indent) {
			indent += "\t";
			report.Add(indent + "Type:       " + exception.GetType().Name);
			report.Add(indent + "Message:    " + exception.Message);
			report.Add(indent + "Data:       " + exception.Data.Count + " entries");
			if(exception is DelayedExceptionList) {
				int i = 0;
				foreach(DelayedException innerException in ((DelayedExceptionList)exception).Exceptions) {
					report.Add(indent + "-------------------");
					report.Add(indent + "Delayed exception " + i + ":");
					report.Add("");
					FormatExceptionReport(innerException, report, indent);
					i++;
				}
			}
			else {
				foreach(object key in exception.Data.Keys) {
					object dataEntry = exception.Data[key];
					string dataEntryString = dataEntry == null ? "null" : dataEntry.ToString();
					report.Add(indent + "\t\t'" + key + "'\t\t'" + dataEntryString + "'");
					if(dataEntry is Exception && dataEntry != exception) {
						FormatExceptionReport((Exception)dataEntry, report, indent + "\t");
					}
				}
				ReflectionTypeLoadException reflectionTypeLoadException = exception as ReflectionTypeLoadException;
				if(reflectionTypeLoadException != null) {
					report.Add(indent + "LoaderExceptions:       " + reflectionTypeLoadException.LoaderExceptions.Length + " entries");
					if(reflectionTypeLoadException.LoaderExceptions.Length > 0) {
						report.Add(indent + "\t\t'0'\t\t'" + reflectionTypeLoadException.LoaderExceptions[0].Message + "'");
					}
				}
				report.Add(indent + "Stack trace:");
				report.Add("");
				report.Add(exception.StackTrace);
				if(exception.InnerException != null) {
					report.Add(indent + "----------------");
					report.Add(indent + "InnerException:");
					report.Add("");
					FormatExceptionReport(exception.InnerException, report, indent + "\t");
				}
				else {
					report.Add(indent + "InnerException is null");
					report.Add(string.Empty);
				}
			}
		}
		protected static void FormatLoadedAssemblies(List<string> report) {
			report.Add(SubSectionDelim);
			report.Add("Loaded assemblies");
			List<string> lines = new List<string>();
			try {
				bool securityExceptionOccurs = false;
				foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					string location = "";
					Boolean isDynamicAssembly = (assembly is System.Reflection.Emit.AssemblyBuilder) || (assembly.GetType().FullName == "System.Reflection.Emit.InternalAssemblyBuilder");
					if(!isDynamicAssembly) {
						if(!securityExceptionOccurs) {
							try {
								location = assembly.Location; 
							}
							catch(SecurityException e) {
								securityExceptionOccurs = true;
								location = e.Message;
							}
						}
					}
					else {
						location = "InMemory Module";
					}
					lines.Add(string.Format("\t{0}, Location={1}", assembly.FullName, location));
				}
			}
			catch(Exception e) {
				lines.Add(e.Message);
			}
			lines.Sort();
			report.AddRange(lines);
		}
		public static string FormatExceptionReportDefault(Exception exception) {
			List<string> report = new List<string>();
			report.Add(SectionDelim);
			report.Add("The error occurred:");
			report.Add("");
			FormatExceptionReport(exception, report, "");
			FormatLoadedAssemblies(report);
			report.Add(SectionDelim);
			report.Add(string.Empty);
			return string.Join("\r\n", report.ToArray());
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public string ValueToString(object value) {
			if(value is Array) {
				return ArrayToString((Array)value, new HashSet<object>());
			}
			return ObjectToString(value);
		}
		public string FormatExceptionReport(Exception exception) {
			return FormatExceptionReportDefault(exception);
		}
		public void LogError(string text) {
			LogError(text, EmptyArgs);
		}
		public virtual void LogError(string text, params object[] args) {
			WriteLineIfFormat(verbositySwitch.TraceError, text, args, true);
		}
		public virtual void LogError(Exception exception) {
			WriteLineIf(verbositySwitch.TraceError, FormatExceptionReport(exception), true, exception);
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public virtual void LogVerboseError(Exception exception) {
			WriteLineIf(verbositySwitch.TraceVerbose, FormatExceptionReport(exception), true, exception);
		}
		public void LogWarning(string text) {
			LogWarning(text, EmptyArgs);
		}
		public virtual void LogWarning(string text, params object[] args) {
			WriteLineIfFormat(verbositySwitch.TraceWarning, text, args);
		}
		public void LogText(string text) {
			LogText(text, EmptyArgs);
		}
		public virtual void LogText(string text, params object[] args) {
			WriteLineIfFormat(verbositySwitch.TraceInfo, text, args);
		}
		public void LogVerboseText(string text) {
			LogVerboseText(text, EmptyArgs);
		}
		public virtual void LogVerboseText(string text, params object[] args) {
			WriteLineIfFormat(verbositySwitch.TraceVerbose, text, args);
		}
		public virtual void LogSetOfStrings(params string[] args) {
			WriteLineIf(verbositySwitch.TraceInfo, string.Join(", ", args));
		}
		public virtual void LogValue(string valueName, object objectValue) {
			LogText(GetMessageByValueCore(valueName, objectValue));
		}
		public void LogVerboseValue(string valueName, object objectValue) {
			LogVerboseText(GetMessageByValueCore(valueName, objectValue));
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public string GetMessageByValue(string valueName, object objectValue, bool useEmptyHeader) {
			string prefix = useEmptyHeader ? Tracing.EmptyHeader : string.Empty;
			return prefix + GetMessageByValueCore(valueName, objectValue) + Environment.NewLine;
		}
		protected virtual string GetMessageByValueCore(string valueName, object objectValue) {
			return string.Format("\t{0}: {1}", valueName, ValueToString(objectValue));
		}
		public virtual void LogLoadedAssemblies() {
			if(verbositySwitch.TraceInfo) {
				List<string> report = new List<string>();
				FormatLoadedAssemblies(report);
				LogText(string.Join("\r\n", report));
			}
		}
		public void LogSeparator(String comment) {
			LogText(GetTopSeparator(comment));
		}
		public void LogSubSeparator(String comment) {
			LogText(GetSubSeparator(comment));
		}
		public void LogVerboseSubSeparator(String comment) {
			LogVerboseText(GetSubSeparator(comment));
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public virtual String GetTopSeparator(String comment) {
			return String.IsNullOrEmpty(comment) ? comment : comment + Environment.NewLine + (new String('=', comment.Length + 1));
		}
		[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public virtual String GetSubSeparator(String comment) {
#pragma warning disable 0618
			return GetSeparator(comment);
#pragma warning restore 0618
		}
		public void LockFlush() {
			lock(internalLockObject) {
				lockCount++;
			}
		}
		public void ResumeFlush() {
			lock(internalLockObject) {
				if(lockCount > 0) {
					lockCount--;
					if(lockCount == 0) {
						FlushCache();
					}
				}
			}
		}
		public int LastEntriesMaxCount {
			get { return lastEntriesMaxCount; }
			set {
				lock(internalLockObject) {
					lastEntriesMaxCount = value;
					lastEntries = new Queue<string>(lastEntriesMaxCount > 0 ? lastEntriesMaxCount : 0);
				}
			}
		}
		public ReadOnlyCollection<string> LastEntries {
			get {
				lock(internalLockObject) {
					return new ReadOnlyCollection<string>(lastEntries.ToArray());
				}
			}
		}
		public string GetLastEntriesAsString() {
			lock(internalLockObject) {
				return string.Join("\n", lastEntries);
			}
		}
		public void Dispose() {
			try {
				lock(Trace.Listeners) {
					if(Trace.Listeners[TraceListenerName] != null) {
						TraceListener listener = Trace.Listeners[TraceListenerName];
						Trace.Listeners.Remove(TraceListenerName);
						listener.Dispose();
					}
				}
			}
			catch {
			}
		}
		public static event EventHandler<NeedContextInformationEventArgs> NeedContextInformation;
		public static event EventHandler<CustomFormatDateTimeStampEventArgs> CustomFormatDateTimeStamp;
		public static event EventHandler<HandledEventArgs> CustomLogStartupInformation;
		public void LogLockedSectionEntering(Type type, string methodName, object lockObject) {
			if(traceLockedSections && verbositySwitch.TraceVerbose) {
				LogVerboseText("Lock section entering :" + type + "." + methodName + ", " + lockObject.GetHashCode());
			}
		}
		public void LogLockedSectionEntered() {
			if(traceLockedSections && verbositySwitch.TraceVerbose) {
				LogVerboseText("Lock section entered");
			}
		}
		public static void LogText(Guid key, string text, params object[] args) {
			tracingDictionary[key].LogText(text, args);
		}
		public static void LogValue(Guid key, string valueName, object objectValue) {
			tracingDictionary[key].LogValue(valueName, objectValue);
		}
		public static void LogError(Guid key, Exception exception) {
			if(tracingDictionary.ContainsKey(key)) {
				tracingDictionary[key].LogError(exception);
			}
		}
		public static event EventHandler<CreateCustomTracerEventArgs> CreateCustomTracer;
#region TODO XAFCore: duplicated with the ReflectionHelper.HasUnmanagedCodePermission/GetAssemblyVersion because this file is linked to the DevExpress.ExpressApp.Updater project
#if !NETSTANDARD && !NETCOREAPP
		private static bool? hasUnmanagedCodePermission = null;
		[SecuritySafeCritical]
		private static bool GetDomainPermission(SecurityPermissionFlag flag) {
			try {
				var permissionSet = new PermissionSet(PermissionState.None);
				permissionSet.AddPermission(new SecurityPermission(flag));
				return permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet);
			}
			catch {
				return false;
			}
		}
		private static bool HasUnmanagedCodePermission {
			get {
				lock(lockObject) {
					if(hasUnmanagedCodePermission == null) {
						hasUnmanagedCodePermission = false;
						try {
							bool tryIsGranted = GetDomainPermission(SecurityPermissionFlag.UnmanagedCode);
							if(tryIsGranted) {
								new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand(); 
								hasUnmanagedCodePermission = true;
							}
						}
						catch {}
					}
				}
				return hasUnmanagedCodePermission.Value;
			}
		}
#endif
#endregion
#region Obsolete 16.2
		[Obsolete("Use the 'GetSubSeparator' method instead."), Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
		public virtual String GetSeparator(String comment) {
			return SubSectionDelim + Environment.NewLine + Tracing.EmptyHeader + comment + Environment.NewLine;
		}
#endregion
	}
}
