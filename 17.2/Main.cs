#region Copyright (c) 2000-2018 Developer Express Inc.
/*
{*******************************************************************}
{                                                                   }
{       Developer Express .NET Component Library                    }
{       eXpressApp Framework                                        }
{                                                                   }
{       Copyright (c) 2000-2018 Developer Express Inc.              }
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
#endregion Copyright (c) 2000-2018 Developer Express Inc.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Win.Utils;
namespace DevExpress.ExpressApp.Updater {
	public class MainClass {
		private static ProgressWindow form;
		private static int GetFilesCountInSubDirectories(string directory) {
			int filesCount = 0;
			string[] subDirectories = Directory.GetDirectories(directory);
			foreach(string subDirectory in subDirectories) {
				filesCount += GetFilesCount(subDirectory);
			}
			return filesCount;
		}
		private static int GetFilesCount(string directory) {
			int filesCount = Directory.GetFiles(directory, "*.*").Length;
			filesCount += GetFilesCountInSubDirectories(directory);
			return filesCount;
		}
		private static ICollection<string> GetSelfFiles(string sourceDirectory) {
			List<string> result = new List<string>();
			result.Add(Path.Combine(sourceDirectory, AppDomain.CurrentDomain.FriendlyName));
			result.Add(Path.Combine(sourceDirectory, Tracing.LogName + ".log"));
			return result;
		}
		private static void CopyNewVersion(string sourceDirectory, string destinationDirectory) {
			if(Directory.Exists(destinationDirectory)) {

                // Custom code
                if (UpdaterHelper.IsFolderIgnored(destinationDirectory))
                    return;
                List<string> updatedDestinationFiles = new List<string>();

				Tracing.Tracer.LogText("CopyNewVersion from '{0}' to '{1}'", sourceDirectory, destinationDirectory);
				string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.*");
				ICollection<string> selfFiles = GetSelfFiles(sourceDirectory);
				foreach(string sourceFileName in sourceFiles) {
					if(!selfFiles.Contains(sourceFileName) && !UpdaterHelper.IsFileIgnored(sourceFileName)) {  // Custom code
						string destinationFileName = Path.Combine(destinationDirectory, Path.GetFileName(sourceFileName));
						if(File.Exists(destinationFileName)) {
							File.SetAttributes(destinationFileName, FileAttributes.Normal);
						}
						File.Copy(sourceFileName, destinationFileName, true);

						// Custom code
						updatedDestinationFiles.Add(destinationFileName);

						Tracing.Tracer.LogText("The \"{0}\" file was copied to \"{1}\".", sourceFileName, destinationFileName);
						form.SetProgressPosition();
					}
				}

				// Custom code
				if (UpdaterHelper.DeleteExistingFiles)
					UpdaterHelper.RemoveNotUpdatedDestinationFiles(destinationDirectory, updatedDestinationFiles);

				UpdateSubDirectories(sourceDirectory, destinationDirectory);
			}
		}
		private static void UpdateSubDirectories(string sourceDirectory, string destinationDirectory) {
			Tracing.Tracer.LogText("Update sub directories from '{0}' to '{1}'", sourceDirectory, destinationDirectory);
			string[] sourceSubDirectories = Directory.GetDirectories(sourceDirectory);

			// Custom code
			List<string> updatedDestinationSubDirectories = new List<string>();

			foreach(string sourceSubDirectory in sourceSubDirectories) {
				string destinationSubDirectory = destinationDirectory + sourceSubDirectory.Remove(0, sourceDirectory.Length);
				if(!Directory.Exists(destinationSubDirectory)) {
					Directory.CreateDirectory(destinationSubDirectory);
					Tracing.Tracer.LogText("Directory '{0}' was created.", destinationSubDirectory);
				}
				CopyNewVersion(sourceSubDirectory, destinationSubDirectory);

				// Custom code
				updatedDestinationSubDirectories.Add(destinationSubDirectory);
			}

			// Custom code
			if (UpdaterHelper.DeleteExistingFiles)
				UpdaterHelper.RemoveNotUpdatedDestinationSubDirectories(updatedDestinationSubDirectories.ToArray(), destinationDirectory);
		}
		private static Boolean CloseAllApplications(string name, int applicationId) {
			if(applicationId != -1) {
				Tracing.Tracer.LogText("try to kill process '{0}'", applicationId);
				Process mainProcess = null;
				try {
					mainProcess = Process.GetProcessById(applicationId);
				} catch(ArgumentException e) {
					Tracing.Tracer.LogText(e.Message);
				}
				if(mainProcess != null) {
					mainProcess.Kill();
					mainProcess.WaitForExit();
				}
			}
			Tracing.Tracer.LogText("Close all applications with name '{0}'", name);
			foreach(Process process in GetCurrentUserProcessByName(name)) {
				Tracing.Tracer.LogText("Try to close process '{0}'", process.Id);
				try {
					while(process.CloseMainWindow()) {
						Thread.Sleep(500);
						process.Refresh();
					}
				} catch(Exception exception) {
					Tracing.Tracer.LogError(exception);
				}
				try {
					process.WaitForExit(3500);
				} catch(Exception exception) {
					Tracing.Tracer.LogError(exception);
				}
			}
			return GetCurrentUserProcessByName(name).Count == 0;
		}
		private static List<Process> GetCurrentUserProcessByName(string name) {
			List<Process> currentUserProcess = new List<Process>();
			foreach(Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name))) {
				if(process.SessionId == Process.GetCurrentProcess().SessionId) { 
					currentUserProcess.Add(process);
				}
			}
			return currentUserProcess;
		}
		[STAThread]
		public static void Main(string[] args) {
			String applicationUpdateCompleteKey = "ApplicationUpdateComplete";
			String customKey = System.Configuration.ConfigurationManager.AppSettings["ApplicationUpdateCompleteKey"];
			if(!string.IsNullOrWhiteSpace(customKey)) {
				applicationUpdateCompleteKey = customKey;
			}
			Tracing.Tracer.LogText("The application will be restarted with the '{0}' key.", applicationUpdateCompleteKey);
			Tracing.Tracer.LogText("args");
			Tracing.Tracer.LogSetOfStrings(args);
			if(args.Length >= 1) {
				String applicationName = "";
				int applicationId = -1;
				if(args.Length > 1) {
					applicationName = args[1];
				}
				if(args.Length > 2) {
					applicationId = Int32.Parse(args[2]);
				}
				if(!String.IsNullOrEmpty(applicationName) && !CloseAllApplications(applicationName, applicationId)) {
					MessageBox.Show(
						"The update process of the starting application cannot be finished, " +
						"because other instances of this application cannot be closed. " +
						"Close these applications manually and start the application again.",
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else {
					try {
						form = new DevExpress.ExpressApp.Win.Utils.ProgressWindow();
						form.Maximum = GetFilesCount(args[0]);
						form.Show();
						CopyNewVersion(args[0], AppDomain.CurrentDomain.BaseDirectory);
					} catch(Exception e) {
						Tracing.Tracer.LogError(e);
						MessageBox.Show(e.Message, "Application Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					finally {
						form.Close();
					}
					if(args.Length > 1) {
						Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, args[1]), applicationUpdateCompleteKey);
					}
				}
			}
		}
	}
}
