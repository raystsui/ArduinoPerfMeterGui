using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domino;
using System.Runtime.InteropServices;
using System.Text;

using DWORD = System.UInt32;
using HANDLE = System.UInt32;
using STATUS = System.UInt16;
// Testing
namespace ArduinoPerfMeterGui
{
    class NotesMailChecker
    {
        private NotesSession ns;
        private NotesDatabase db;
        private NotesDocument doc;

        [DllImport("nnotes.dll")]
        public static extern STATUS OSPathNetConstruct(string port, string server, string filename, StringBuilder retFullpathName);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbOpen(string path, out HANDLE phDB);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbClose(HANDLE hDb);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbGetUnreadNoteTable(HANDLE hDb, string user, ushort namelen, bool create, out HANDLE hList);

        [DllImport("nnotes.dll")]
        public static extern STATUS NSFDbUpdateUnread(HANDLE hDb, HANDLE hUnreadList);

        //[DllImport("nnotes.d ll")]
        //public static extern bool IDIsPresent(HANDLE hTable, DWORD id);

        [DllImport("nnotes.dll")]
        public static extern bool IDScan(HANDLE hTable, bool first, out HANDLE id);

        [DllImport("nnotes.dll")]
        public static extern STATUS OSMemFree(HANDLE h);

        [DllImport("nnotes.dll")]
        public static extern STATUS IDDestroyTable(HANDLE h);



        // Return number of unread mail.
        public int getUnreadMail() {
            int result = 0;
            return result;
        }

        // Return number of unread mail with ID not in the previous unread ID list. FALSE otherwise.
        public bool getNewUnreadMail() {
            int result = 0;
            return result;
        }




    }
}
