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
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace DevExpress.ExpressApp.Win.Utils {
	public class ProgressWindow : Form {
		private ProgressBar progressBar;
		private const int IDI_APPLICATION = 32512;
		[DllImport("KERNEL32.DLL", EntryPoint = "GetModuleHandle", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
		private static extern IntPtr GetModuleHandle(string moduleName);
		[DllImport("USER32.DLL", EntryPoint = "LoadIcon", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.StdCall)]
		private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr name);
		[System.Security.SecuritySafeCritical]
		public Icon GetExecutingApplicationIcon() {
			IntPtr hndl = LoadIcon(GetModuleHandle(AppDomain.CurrentDomain.FriendlyName), new IntPtr(IDI_APPLICATION));
			if(hndl != new IntPtr(0))
				return Icon.FromHandle(hndl);
			return null;
		}
		public ProgressWindow() {
			int Padding = 10;
			StartPosition = FormStartPosition.CenterScreen;
			FormBorderStyle = FormBorderStyle.None;
			MinimizeBox = false;
			MaximizeBox = false;
			HelpButton = false;
			ShowInTaskbar = true;
			Icon = GetExecutingApplicationIcon();
			Text = Application.ProductName;
			Panel place = new Panel();
			place.Location = new Point(0, 0);
			place.Dock = DockStyle.Fill;
			place.BorderStyle = BorderStyle.FixedSingle;
			Controls.Add(place);
			PictureBox picture = new PictureBox();
			picture.SizeMode = PictureBoxSizeMode.AutoSize;
			picture.Image = Icon.ToBitmap();
			picture.Location = new System.Drawing.Point(Padding, Padding);
			place.Controls.Add(picture);
			Label waitLabel = new Label();
			waitLabel.AutoSize = true;
			waitLabel.Location = new System.Drawing.Point(Padding * 2 + picture.Width, 0);
			waitLabel.Text = "  Updating application to the newest version...  ";
			place.Controls.Add(waitLabel);
			Size = new Size(picture.Width + waitLabel.Width + Padding * 3, picture.Height + Padding * 2 + 10);
			waitLabel.Top = (picture.Height - waitLabel.Height) / 2 + Padding - 5;
			progressBar = new System.Windows.Forms.ProgressBar();
			progressBar.Minimum = 0;
			progressBar.Value = 0;
			progressBar.Step = 1;
			progressBar.Location = new System.Drawing.Point(Padding * 2 + picture.Width + 8, 35);
			progressBar.Size = new Size(waitLabel.Width - 15, 10);
			place.Controls.Add(progressBar);
		}
		public int Maximum {
			get { return progressBar.Maximum; }
			set { progressBar.Maximum = value; }
		}
		public void SetProgressPosition() {
			progressBar.Value++;
			Application.DoEvents();
		}
		public void SetProgressPosition(int maximum, int currentPosition) {
			progressBar.Maximum = maximum;
			progressBar.Value = currentPosition;
			Application.DoEvents();
		}
	}
}
