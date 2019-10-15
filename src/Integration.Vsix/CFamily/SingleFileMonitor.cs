﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio;
using SonarLint.VisualStudio.Integration.Helpers;

namespace SonarLint.VisualStudio.Integration.Vsix.CFamily
{
    /// <summary>
    /// Monitors a single file for all types of change (creation, modification, deletion, rename)
    /// and raises an event for any change.
    /// </summary>
    /// <remarks>
    /// Duplicate events can be raised by the lower-level file system watcher class - see
    /// https://stackoverflow.com/questions/1764809/filesystemwatcher-changed-event-is-raised-twice
    /// This wrapper class ensures that duplicate notifications will not be passed on to clients.
    /// However, if notifications occur very close together then some of them may be lost.
    /// In other words, we're removing duplicates but at the cost of potentially losing a few real
    /// events.
    /// </remarks>
    internal sealed class SingleFileMonitor : IDisposable
    {
        private readonly string filePathToMonitor;
        private readonly IFileSystemWatcher fileWatcher;
        private readonly ILogger logger;
        private DateTime lastWriteTime = DateTime.MinValue;

        public SingleFileMonitor(string filePathToMonitor, ILogger logger)
            : this(new FileSystemWatcherWrapperFactory(), filePathToMonitor, logger)
        {
        }

        public SingleFileMonitor(IFileSystemWatcherFactory factory, string filePathToMonitor, ILogger logger)
        {
            this.filePathToMonitor = filePathToMonitor;

            fileWatcher = factory.Create();
            fileWatcher.Path = Path.GetDirectoryName(filePathToMonitor);
            fileWatcher.Filter = Path.GetFileName(filePathToMonitor);
            fileWatcher.NotifyFilter = System.IO.NotifyFilters.CreationTime | System.IO.NotifyFilters.LastWrite |
                NotifyFilters.FileName | NotifyFilters.DirectoryName;

            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Created += OnFileChanged;
            fileWatcher.Deleted += OnFileChanged;
            fileWatcher.Renamed += (s, args) => OnFileChanged(s, args);

            this.logger = logger;
        }

        private EventHandler fileChangedHandlers;
        public event EventHandler FileChanged
        {
            add
            {
                fileChangedHandlers += value;
                fileWatcher.EnableRaisingEvents = true;
            }
            remove
            {
                fileChangedHandlers -= value;
                if (fileChangedHandlers == null)
                {
                    fileWatcher.EnableRaisingEvents = false;
                }
            }
        }

        private void OnFileChanged(object sender, System.IO.FileSystemEventArgs args)
        {
            Debug.Assert(fileChangedHandlers != null, "Not expecting file system events to be monitored if there are no listeners");
            if (fileChangedHandlers == null)
            {
                return;
            }

            try
            {
                fileWatcher.EnableRaisingEvents = false;

                // We're trying to ignore duplicate events by checking the last-write time.
                // However, the precision of DateTime means that it is possible for separate events
                // that happen very close together to report the same last-write time. If that happens,
                // we'll be ignoring a "real" notification.
                var currentTime = File.GetLastWriteTimeUtc(args.FullPath);
                if (args.ChangeType != WatcherChangeTypes.Renamed && currentTime == lastWriteTime)
                {
                    logger.WriteLine($"Ignoring duplicate change event: {args.ChangeType}");
                    return;
                }

                lastWriteTime = currentTime;
                logger.WriteLine(CFamilyStrings.FileMonitor_FileChanged, filePathToMonitor, args.ChangeType.ToString() + System.Threading.Thread.CurrentThread.ManagedThreadId);

                fileChangedHandlers(this, EventArgs.Empty);
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.WriteLine(CFamilyStrings.FileMonitor_ErrorHandlingFileChange, ex.Message);
            }
            finally
            {
                fileWatcher.EnableRaisingEvents = true;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fileWatcher.Dispose();
                    fileChangedHandlers = null;
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
