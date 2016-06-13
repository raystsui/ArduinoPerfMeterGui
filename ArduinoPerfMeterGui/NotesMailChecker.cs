using Domino;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HANDLE = System.UInt32;
using STATUS = System.UInt16;

namespace ArduinoPerfMeterGui
{
    internal class NotesMailChecker
    {
        private enum ProgramState
        {
            Start,
            Initializing,
            Connected,
            Disconnected,
            Waiting,
            End
        }

        private ProgramState programState;
        private NotesSession ns;
        private HANDLE hNotesDB;
        private HANDLE hUnreadListTable;

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

        public NotesMailChecker()
        {
            programState = ProgramState.Start;

            RaymondsInit();
        }

        private void RaymondsInit()
        {
            programState = ProgramState.Initializing;
            hNotesDB = OpenNotesDatabase();
            if (hNotesDB != 0)
            {
                programState = ProgramState.Connected;
            }
        }

        private HANDLE OpenNotesDatabase()
        {
            HANDLE hDb = 0;

            try
            {
                ns = new NotesSession();
                ns.Initialize("password");
                string mailServer = ns.GetEnvironmentString("MailServer", true);
                string mailFile = ns.GetEnvironmentString("MailFile", true);
                string userName = ns.UserName;

                StringBuilder fullpathName = new StringBuilder(512);
                OSPathNetConstruct(null, mailServer, mailFile, fullpathName);

                Debug.WriteLine($"mailServer: {mailServer}");
                Debug.WriteLine($"mailFile: {mailFile}");
                Debug.WriteLine($"userName: {userName}");
                Debug.WriteLine($"fullpathName: {fullpathName.ToString()}");

                NSFDbOpen(fullpathName.ToString(), out hDb);
                Debug.WriteLine($"hNotesDB: {hNotesDB.ToString()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenNotesDatabase: {ex.ToString()}");
            }

            return hDb;
        }

        private void CloseNotesDatabase(HANDLE hDb)
        {
            n
        }

        // Return number of unread mail.
        public int getUnreadMail()
        {
            int result = 0;
            return result;
        }

        // Return number of unread mail with ID not in the previous unread ID list. FALSE otherwise.
        public int getNewUnreadMail()
        {
            int result = 0;
            return result;
        }
    }
}