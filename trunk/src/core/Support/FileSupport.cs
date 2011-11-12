﻿using System;
using System.IO;

namespace Lucene.Net.Support
{
    /// <summary>
    /// Represents the methods to support some operations over files.
    /// </summary>
    public class FileSupport
    {
        /// <summary>
        /// Returns an array of abstract pathnames representing the files and directories of the specified path.
        /// </summary>
        /// <param name="path">The abstract pathname to list it childs.</param>
        /// <returns>An array of abstract pathnames childs of the path specified or null if the path is not a directory</returns>
        public static System.IO.FileInfo[] GetFiles(System.IO.FileInfo path)
        {
            if ((path.Attributes & FileAttributes.Directory) > 0)
            {
                String[] fullpathnames = Directory.GetFileSystemEntries(path.FullName);
                System.IO.FileInfo[] result = new System.IO.FileInfo[fullpathnames.Length];
                for (int i = 0; i < result.Length; i++)
                    result[i] = new System.IO.FileInfo(fullpathnames[i]);
                return result;
            }
            else
                return null;
        }

        /// <summary>
        /// Returns a list of files in a give directory.
        /// </summary>
        /// <param name="fullName">The full path name to the directory.</param>
        /// <param name="indexFileNameFilter"></param>
        /// <returns>An array containing the files.</returns>
        public static System.String[] GetLuceneIndexFiles(System.String fullName,
                                                          Index.IndexFileNameFilter indexFileNameFilter)
        {
            System.IO.DirectoryInfo dInfo = new System.IO.DirectoryInfo(fullName);
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            foreach (System.IO.FileInfo fInfo in dInfo.GetFiles())
            {
                if (indexFileNameFilter.Accept(fInfo, fInfo.Name) == true)
                {
                    list.Add(fInfo.Name);
                }
            }
            System.String[] retFiles = new System.String[list.Count];
            list.CopyTo(retFiles);
            return retFiles;
        }

        // Disable the obsolete warning since we must use FileStream.Handle
        // because Mono does not support FileSystem.SafeFileHandle at present.
#pragma warning disable 618

        /// <summary>
        /// Flushes the specified file stream. Ensures that all buffered
        /// data is actually written to the file system.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        public static void Sync(System.IO.FileStream fileStream)
        {
            if (fileStream == null)
                throw new ArgumentNullException("fileStream");

            fileStream.Flush();

            //if (OS.IsWindows)
            //{
            //    if (!FlushFileBuffers(fileStream.Handle))
            //        throw new System.IO.IOException();
            //}
            //else if (OS.IsUnix)
            //{
            //    if (fsync(fileStream.Handle) != IntPtr.Zero)
            //    throw new System.IO.IOException();
            //}
            //else
            //{
            //    throw new NotImplementedException();
            //}
        }

#pragma warning restore 618

        //[System.Runtime.InteropServices.DllImport("libc")]
        //extern static IntPtr fsync(IntPtr fd);

        //[System.Runtime.InteropServices.DllImport("kernel32.dll")]
        //extern static bool FlushFileBuffers(IntPtr hFile);
    }
}